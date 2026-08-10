using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;

namespace Kst.Acoustics.App.ViewModels;

public sealed record DocumentHistoryEntryInfo(
    int Index,
    DateTime CreatedAtUtc,
    string Description,
    bool IsCurrent);

public sealed partial class ProjectWindowViewModel
{
    private const int MaximumDocumentHistoryEntries = 50;
    private static readonly TimeSpan DefaultAutoSaveInterval = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions HistoryFingerprintOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new HistoryDoubleJsonConverter(),
            new JsonStringEnumConverter()
        }
    };

    private readonly List<DocumentHistoryEntry> _documentHistory = [];
    private ProjectVersionHistoryService _versionHistoryService = null!;
    private int _documentHistoryIndex = -1;
    private string? _savedDocumentFingerprint;
    private bool _isDocumentHistoryReplay;
    private int _historyTransactionDepth;
    private bool _historyTransactionDirty;
    private string _historyTransactionDescription = "Proje değiştirildi";
    private CancellationTokenSource? _autoSaveCts;
    private PeriodicTimer? _autoSaveTimer;
    private Task? _autoSaveLoop;
    private long _lastAutoSavedRevision = -1;
    private readonly SemaphoreSlim _autoSaveGate = new(1, 1);
    private bool _historyDisposed;

    internal bool IsDocumentHistoryReplay => _isDocumentHistoryReplay;

    public bool CanUndoDocument => _documentHistoryIndex > 0;
    public bool CanRedoDocument =>
        _documentHistoryIndex >= 0 && _documentHistoryIndex < _documentHistory.Count - 1;

    public string UndoDocumentDescription => CanUndoDocument
        ? _documentHistory[_documentHistoryIndex].Description
        : string.Empty;

    public string RedoDocumentDescription => CanRedoDocument
        ? _documentHistory[_documentHistoryIndex + 1].Description
        : string.Empty;

    public IReadOnlyList<DocumentHistoryEntryInfo> DocumentHistory =>
        _documentHistory.Select((entry, index) => new DocumentHistoryEntryInfo(
            index,
            entry.CreatedAtUtc,
            entry.Description,
            index == _documentHistoryIndex)).ToArray();

    public ObservableCollection<ProjectVersionInfo> ProjectVersionHistory { get; } = [];

    public bool IsAutoSaveRunning
    {
        get => _isAutoSaveRunning;
        private set => SetProperty(ref _isAutoSaveRunning, value);
    }
    private bool _isAutoSaveRunning;

    public string AutoSaveStatusText
    {
        get => _autoSaveStatusText;
        private set => SetProperty(ref _autoSaveStatusText, value);
    }
    private string _autoSaveStatusText = "Otomatik kayıt henüz çalışmadı.";

    public DateTime? LastAutoSaveAtUtc
    {
        get => _lastAutoSaveAtUtc;
        private set => SetProperty(ref _lastAutoSaveAtUtc, value);
    }
    private DateTime? _lastAutoSaveAtUtc;

    private void InitializeDocumentHistory(ProjectVersionHistoryService? versionHistoryService)
    {
        _versionHistoryService = versionHistoryService ?? new ProjectVersionHistoryService();
        if (!TryCaptureCurrentDocument("Başlangıç", out var entry)) return;

        _documentHistory.Add(entry);
        _documentHistoryIndex = 0;
        _savedDocumentFingerprint = entry.Fingerprint;
        NotifyDocumentHistoryChanged();
    }

    /// <summary>
    /// Birden fazla MarkDirty çağrısı üreten bileşik işlemleri tek açıklamalı geçmiş
    /// adımında birleştirir. Scope dispose edildiğinde son durum yakalanır.
    /// </summary>
    public IDisposable BeginDocumentChange(string description)
    {
        if (_historyDisposed) throw new ObjectDisposedException(nameof(ProjectWindowViewModel));
        _historyTransactionDepth++;
        if (_historyTransactionDepth == 1)
        {
            _historyTransactionDirty = false;
            _historyTransactionDescription = NormalizeHistoryDescription(description);
        }
        return new DocumentChangeScope(this);
    }

    /// <summary>Kalıcı bir değişikliği dirty/revision/geçmiş zincirine birlikte işler.</summary>
    public void CommitDocumentChange(string description) => MarkDirty(description);

    internal void RecordDocumentHistory(string description)
    {
        if (_isDocumentHistoryReplay || _historyDisposed) return;
        if (_historyTransactionDepth > 0)
        {
            _historyTransactionDirty = true;
            return;
        }

        CaptureAndAppendDocumentHistory(description);
    }

    internal void MarkDocumentHistoryBaseline()
    {
        if (_historyDisposed || _isDocumentHistoryReplay) return;
        if (!TryCaptureCurrentDocument("Kaydedilen durum", out var entry)) return;

        AppendDocumentHistory(entry);
        _savedDocumentFingerprint = entry.Fingerprint;
        _lastAutoSavedRevision = _revision;
        NotifyDocumentHistoryChanged();
    }

    internal void ForgetDocumentHistoryBaseline()
    {
        _savedDocumentFingerprint = null;
        NotifyDocumentHistoryChanged();
    }

    public bool UndoDocument()
    {
        if (!CanUndoDocument || _historyDisposed) return false;

        var undoneDescription = _documentHistory[_documentHistoryIndex].Description;
        _documentHistoryIndex--;
        RestoreDocumentHistoryEntry(_documentHistory[_documentHistoryIndex]);
        StatusMessage = $"Geri alındı: {undoneDescription}";
        return true;
    }

    public bool RedoDocument()
    {
        if (!CanRedoDocument || _historyDisposed) return false;

        _documentHistoryIndex++;
        var entry = _documentHistory[_documentHistoryIndex];
        RestoreDocumentHistoryEntry(entry);
        StatusMessage = $"Yinelendi: {entry.Description}";
        return true;
    }

    public void StartAutoSave(TimeSpan? interval = null)
    {
        if (_historyDisposed) throw new ObjectDisposedException(nameof(ProjectWindowViewModel));
        var effectiveInterval = interval ?? DefaultAutoSaveInterval;
        if (effectiveInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Otomatik kayıt aralığı pozitif olmalıdır.");

        StopAutoSave();
        var cts = new CancellationTokenSource();
        var timer = new PeriodicTimer(effectiveInterval);
        _autoSaveCts = cts;
        _autoSaveTimer = timer;
        _autoSaveLoop = RunAutoSaveLoopAsync(timer, cts.Token);
        AutoSaveStatusText = $"Otomatik kayıt etkin ({effectiveInterval.TotalMinutes:G3} dk).";
    }

    public void StopAutoSave()
    {
        var cts = Interlocked.Exchange(ref _autoSaveCts, null);
        var timer = Interlocked.Exchange(ref _autoSaveTimer, null);
        if (cts is not null)
        {
            try { cts.Cancel(); } catch (ObjectDisposedException) { }
        }
        timer?.Dispose();
        cts?.Dispose();
    }

    public async Task<ProjectVersionInfo?> CreateProjectVersionAsync(
        string description = "Elle oluşturulan sürüm",
        CancellationToken cancellationToken = default)
    {
        if (_historyDisposed) return null;
        await _autoSaveGate.WaitAsync(cancellationToken);
        try
        {
            if (_historyDisposed) return null;
            IsAutoSaveRunning = true;
            var revisionAtCapture = Interlocked.Read(ref _revision);
            AcousticsProject snapshot;
            try
            {
                snapshot = CloneDocumentSnapshot(BuildCurrentProject());
            }
            catch (InvalidDataException ex)
            {
                AutoSaveStatusText = $"Otomatik kayıt bekliyor: {ex.Message}";
                return null;
            }

            var version = await _versionHistoryService.SaveVersionAsync(
                snapshot,
                _savedFilePath,
                NormalizeHistoryDescription(description),
                cancellationToken);

            if (_historyDisposed || cancellationToken.IsCancellationRequested)
                return null;

            if (revisionAtCapture == Interlocked.Read(ref _revision))
                _lastAutoSavedRevision = revisionAtCapture;
            LastAutoSaveAtUtc = version.CreatedAtUtc;
            AutoSaveStatusText = $"Otomatik kayıt: {version.CreatedAtUtc.ToLocalTime():g}";
            var versions = await _versionHistoryService.ListVersionsAsync(
                snapshot, _savedFilePath, cancellationToken);
            ReplaceProjectVersionHistory(versions);
            return version;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            AutoSaveStatusText = $"Otomatik kayıt başarısız: {ex.Message}";
            return null;
        }
        finally
        {
            if (_historyDisposed) _isAutoSaveRunning = false;
            else IsAutoSaveRunning = false;
            _autoSaveGate.Release();
        }
    }

    public async Task RefreshProjectVersionHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (_historyDisposed) return;
        AcousticsProject current;
        try
        {
            current = CloneDocumentSnapshot(BuildCurrentProject());
        }
        catch (InvalidDataException) when (_documentHistoryIndex >= 0)
        {
            // Geçici olarak hatalı bir nokta satırı varken bile son geçerli sürüm
            // listesini gösterebiliriz; yeni snapshot üretmek ise düzeltilene dek
            // engellenmeye devam eder.
            current = _documentHistory[_documentHistoryIndex].Snapshot;
        }
        var versions = await _versionHistoryService.ListVersionsAsync(
            current, _savedFilePath, cancellationToken);
        ReplaceProjectVersionHistory(versions);
    }

    private void ReplaceProjectVersionHistory(IReadOnlyList<ProjectVersionInfo> versions)
    {
        ProjectVersionHistory.Clear();
        foreach (var version in versions)
            ProjectVersionHistory.Add(version);
    }

    public async Task<bool> RestoreProjectVersionAsync(
        ProjectVersionInfo version,
        CancellationToken cancellationToken = default)
    {
        if (_historyDisposed) return false;
        try
        {
            var snapshot = await _versionHistoryService.RestoreVersionAsync(version, cancellationToken);
            ApplyDocumentSnapshot(snapshot);
            MarkDirty($"Sürüm geri yüklendi: {version.Description}");
            StatusMessage = $"{version.CreatedAtUtc.ToLocalTime():g} tarihli sürüm geri yüklendi.";
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusMessage = $"Sürüm geri yüklenemedi: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteProjectVersionAsync(
        ProjectVersionInfo version,
        CancellationToken cancellationToken = default)
    {
        if (_historyDisposed) return false;
        try
        {
            await _versionHistoryService.DeleteVersionAsync(version, cancellationToken);
            ProjectVersionHistory.Remove(version);
            StatusMessage = "Sürüm kaydı silindi.";
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusMessage = $"Sürüm silinemedi: {ex.Message}";
            return false;
        }
    }

    private async Task RunAutoSaveLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!IsDirty || Interlocked.Read(ref _revision) == _lastAutoSavedRevision)
                    continue;
                await CreateProjectVersionAsync("Otomatik kayıt", cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Pencere kapandı veya otomatik kayıt yeniden başlatıldı.
        }
    }

    private void EndDocumentChange()
    {
        if (_historyTransactionDepth <= 0) return;
        _historyTransactionDepth--;
        if (_historyTransactionDepth > 0) return;

        if (!_historyTransactionDirty)
        {
            _revision++;
            IsDirty = true;
        }
        CaptureAndAppendDocumentHistory(_historyTransactionDescription);
        _historyTransactionDirty = false;
    }

    private void CaptureAndAppendDocumentHistory(string description)
    {
        if (!TryCaptureCurrentDocument(description, out var entry)) return;
        AppendDocumentHistory(entry);
    }

    private void AppendDocumentHistory(DocumentHistoryEntry entry)
    {
        if (_documentHistoryIndex >= 0 &&
            string.Equals(_documentHistory[_documentHistoryIndex].Fingerprint, entry.Fingerprint, StringComparison.Ordinal))
        {
            NotifyDocumentHistoryChanged();
            return;
        }

        if (_documentHistoryIndex < _documentHistory.Count - 1)
            _documentHistory.RemoveRange(
                _documentHistoryIndex + 1,
                _documentHistory.Count - _documentHistoryIndex - 1);

        _documentHistory.Add(entry);
        _documentHistoryIndex = _documentHistory.Count - 1;
        while (_documentHistory.Count > MaximumDocumentHistoryEntries)
        {
            _documentHistory.RemoveAt(0);
            _documentHistoryIndex--;
        }

        NotifyDocumentHistoryChanged();
    }

    private bool TryCaptureCurrentDocument(string description, out DocumentHistoryEntry entry)
    {
        try
        {
            var snapshot = CloneDocumentSnapshot(BuildCurrentProject());
            entry = new DocumentHistoryEntry(
                snapshot,
                ComputeDocumentFingerprint(snapshot),
                DateTime.UtcNow,
                NormalizeHistoryDescription(description));
            return true;
        }
        catch (InvalidDataException)
        {
            // Geçersiz metin alanları dirty kalır fakat kalıcı domain snapshot'ına
            // çevrilemez. Kullanıcı düzeltince yeni geçerli durum kaydedilir; Undo son
            // geçerli adıma döner.
            entry = null!;
            NotifyDocumentHistoryChanged();
            return false;
        }
    }

    private void RestoreDocumentHistoryEntry(DocumentHistoryEntry entry)
    {
        ApplyDocumentSnapshot(entry.Snapshot);
        _revision++;
        InvalidateModelHealthAnalysis();
        IsDirty = _savedDocumentFingerprint is null ||
                  !string.Equals(entry.Fingerprint, _savedDocumentFingerprint, StringComparison.Ordinal);
        if (!IsDirty) _savedRevision = _revision;
        NotifyDocumentHistoryChanged();
    }

    private void ApplyDocumentSnapshot(AcousticsProject source)
    {
        var snapshot = CloneDocumentSnapshot(source);
        _isDocumentHistoryReplay = true;
        try
        {
            CancelActiveSimulation();
            _workingSurfaces = snapshot.Surfaces
                .Select(s => s with { Vertices = s.Vertices.ToArray() })
                .ToList();
            SelectedSurfaceIds = [];
            HighlightedSurfaceId = null;
            _lastTransformSnapshot = null;
            _lastPanelTransformSnapshot = null;
            CanUndo = false;

            RebuildCategoryNodes();
            RestoreCategoryMaterialRows(snapshot.CategoryMaterials);
            RestoreSurfaceMaterialOverrides(snapshot.SurfaceMaterials);
            LoadPoints(snapshot.Sources, snapshot.Receivers);

            _volumeInitialized = false;
            _roomVolume = snapshot.RoomVolume;
            _roomVolumeOrigin = snapshot.VolumeOrigin ?? RoomVolumeOrigin.BoundingBoxEstimate;
            RoomVolumeSourceText = BuildRoomVolumeSourceText(_roomVolumeOrigin);
            OnPropertyChanged(nameof(RoomVolume));
            _volumeInitialized = true;

            RestoreSimulationSettings(snapshot.SimulationSettings ?? snapshot.LastResult?.Parameters);
            RestorePanelEditorState(snapshot.PanelKonfigurasyon);

            _previousResult = null;
            if (snapshot.LastResult is { } storedResult)
            {
                var result = CloneSimulationResult(storedResult);
                ValidateRestoredSimulationResult(snapshot, result);
                OnSimulationCompleted(result);
            }
            else
            {
                ClearSimulationResults("Bu geçmiş adımında simülasyon sonucu yok.");
            }

            // Proje kökü baseline durumunu, etkin senaryo ise gerçek çalışma
            // durumunu taşır. Senaryo listesi/aktif kimliği de aynı history adımıyla
            // geri alınmalı; helper sonucu zorla stale yapmadan etkin state'i uygular.
            RestoreScenarioPersistence(snapshot, applyWorkingState: true);

            OnPropertyChanged(nameof(ProjectSurfaces));
            NotifyViewportPerformanceChanged();
            RefreshLightTracingOptions();
            RefreshSelectedSurfaces();
            RefreshMaterialStatus();
            RefreshSabineCalculation();
            OnPropertyChanged(nameof(CanRunSimulation));
            OnPropertyChanged(nameof(HasSimulationBlockingIssues));
            OnPropertyChanged(nameof(SimulationPreflightText));
        }
        finally
        {
            _volumeInitialized = true;
            _isDocumentHistoryReplay = false;
        }
    }

    private void RestoreCategoryMaterialRows(IReadOnlyDictionary<string, string> materialMap)
    {
        CategoryMaterialRows.Clear();
        foreach (var node in CategoryNodes)
        {
            materialMap.TryGetValue(node.Name, out var materialId);
            var resolved = materialId is null
                ? null
                : MaterialDatabase.Instance.FindById(materialId) ?? MaterialLibrary.FindById(materialId);
            var row = new CategoryMaterialViewModel(
                node.Name,
                node.Count,
                resolved ?? MaterialLibrary.Default,
                unresolvedMaterialId: materialId is not null && resolved is null ? materialId : null);
            row.PropertyChanged += (sender, args) =>
            {
                RefreshMaterialStatus();
                if (args.PropertyName == nameof(CategoryMaterialViewModel.SelectedMaterial) &&
                    sender is CategoryMaterialViewModel changed)
                    OnCategoryMaterialChanged(changed.Category);
            };
            CategoryMaterialRows.Add(row);
        }
    }

    private void RestoreSurfaceMaterialOverrides(IReadOnlyDictionary<string, string> materialMap)
    {
        _surfaceMaterialOverrides.Clear();
        foreach (var pair in materialMap)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value))
                _surfaceMaterialOverrides[pair.Key] = pair.Value;
        }

        SurfaceMaterialCategoryFilters.Clear();
        SurfaceMaterialCategoryFilters.Add(SurfaceMaterialFilterAll);
        foreach (var category in _workingSurfaces
                     .Select(s => s.Category)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            SurfaceMaterialCategoryFilters.Add(category);
        _selectedSurfaceMaterialCategoryFilter = SurfaceMaterialFilterAll;
        OnPropertyChanged(nameof(SelectedSurfaceMaterialCategoryFilter));
        RebuildSurfaceMaterialRows();
    }

    private void RestorePanelEditorState(AcousticPanelKonfigurasyon? source)
    {
        foreach (var row in PanelListesi)
            row.PropertyChanged -= PanelRow_PropertyChanged;
        PanelListesi.Clear();
        PanelKonfigurasyon = ClonePanelConfiguration(source);

        var surfaceNames = GetPanelHostSurfaces()
            .Select(s => s.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SahneOnuSecenekleri.Clear();
        foreach (var name in surfaceNames) SahneOnuSecenekleri.Add(name);
        HedefYuzeySecenekleri.Clear();
        HedefYuzeySecenekleri.Add(HedefYuzeyOtomatikEtiket);
        foreach (var name in surfaceNames) HedefYuzeySecenekleri.Add(name);

        if (PanelKonfigurasyon is { } config)
        {
            PanelEniM = config.PanelEniM;
            SiraliPanelEniAktif = config.PanelEniModu == PanelEniModu.Sirali;
            SetPanelEniRows(config.PanelEnleriM.Count > 0 ? config.PanelEnleriM : [config.PanelEniM]);
            SarkmaCikmaM = config.SarkmaCikmaM;
            TavanPanelAktif = config.AktifTipler.Contains(PanelTipi.Tavan);
            SolDuvarPanelAktif = config.AktifTipler.Contains(PanelTipi.DuvarSol);
            SagDuvarPanelAktif = config.AktifTipler.Contains(PanelTipi.DuvarSag);
            SeciliSahneOnu = config.SahneOnuYuzeyId;
            SeciliTavanYuzey = SelectPanelHostOption(config.TavanYuzeyId);
            SeciliSolDuvarYuzey = SelectPanelHostOption(config.SolDuvarYuzeyId);
            SeciliSagDuvarYuzey = SelectPanelHostOption(config.SagDuvarYuzeyId);
            foreach (var panel in config.Paneller) AddPanelRow(panel);
        }
        else
        {
            PanelEniM = 0.6;
            SiraliPanelEniAktif = false;
            SetPanelEniRows([PanelEniM]);
            SarkmaCikmaM = 0.5;
            TavanPanelAktif = true;
            SolDuvarPanelAktif = true;
            SagDuvarPanelAktif = true;
            SeciliSahneOnu = SahneOnuSecenekleri.FirstOrDefault();
            SeciliTavanYuzey = HedefYuzeyOtomatikEtiket;
            SeciliSolDuvarYuzey = HedefYuzeyOtomatikEtiket;
            SeciliSagDuvarYuzey = HedefYuzeyOtomatikEtiket;
        }
        RefreshHedefYuzeyOzeti();
    }

    private string SelectPanelHostOption(string? value) =>
        !string.IsNullOrWhiteSpace(value) && HedefYuzeySecenekleri.Contains(value)
            ? value
            : HedefYuzeyOtomatikEtiket;

    private static void ValidateRestoredSimulationResult(
        AcousticsProject project,
        SimulationResult result)
    {
        if (result.IsStale) return;
        if (string.IsNullOrWhiteSpace(result.InputFingerprint))
        {
            result.IsStale = true;
            result.StaleReason = "Geri yüklenen sonuçta girdi izi yok; güncelliği doğrulanamadı.";
            return;
        }

        var settings = project.SimulationSettings ?? result.Parameters;
        var fingerprint = SimulationInputFingerprint.Compute(project, settings);
        if (!string.Equals(fingerprint, result.InputFingerprint, StringComparison.Ordinal))
        {
            result.IsStale = true;
            result.StaleReason = "Geri yüklenen sonuç ile proje girdilerinin izi eşleşmiyor.";
        }
    }

    private static AcousticsProject CloneDocumentSnapshot(AcousticsProject source) =>
        source with
        {
            Surfaces = source.Surfaces
                .Select(s => s with { Vertices = s.Vertices.ToArray() })
                .ToArray(),
            Sources = source.Sources.ToArray(),
            Receivers = source.Receivers.ToArray(),
            CategoryMaterials = new Dictionary<string, string>(
                source.CategoryMaterials, StringComparer.OrdinalIgnoreCase),
            SurfaceMaterials = new Dictionary<string, string>(
                source.SurfaceMaterials, StringComparer.OrdinalIgnoreCase),
            ImportWarnings = source.ImportWarnings.ToArray(),
            PanelKonfigurasyon = ClonePanelConfiguration(source.PanelKonfigurasyon),
            LastResult = source.LastResult is null ? null : CloneSimulationResult(source.LastResult),
            Scenarios = source.Scenarios.Select(AcousticScenarioCloner.Clone).ToArray()
        };

    private static string ComputeDocumentFingerprint(AcousticsProject project)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(project, HistoryFingerprintOptions);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string NormalizeHistoryDescription(string? description)
    {
        var value = string.IsNullOrWhiteSpace(description) ? "Proje değiştirildi" : description.Trim();
        return value.Length <= 160 ? value : value[..160];
    }

    private void NotifyDocumentHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndoDocument));
        OnPropertyChanged(nameof(CanRedoDocument));
        OnPropertyChanged(nameof(UndoDocumentDescription));
        OnPropertyChanged(nameof(RedoDocumentDescription));
        OnPropertyChanged(nameof(DocumentHistory));
    }

    private void DisposeDocumentHistory()
    {
        if (_historyDisposed) return;
        _historyDisposed = true;
        StopAutoSave();
    }

    private sealed record DocumentHistoryEntry(
        AcousticsProject Snapshot,
        string Fingerprint,
        DateTime CreatedAtUtc,
        string Description);

    private sealed class DocumentChangeScope(ProjectWindowViewModel owner) : IDisposable
    {
        private ProjectWindowViewModel? _owner = owner;

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            current?.EndDocumentChange();
        }
    }

    private sealed class HistoryDoubleJsonConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? double.NaN : reader.GetDouble();

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsFinite(value)) writer.WriteNumberValue(value);
            else writer.WriteNullValue();
        }
    }
}
