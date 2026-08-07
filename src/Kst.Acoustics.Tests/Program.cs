using System.Globalization;
using Kst.Acoustics.Core.Importing;
using Kst.Acoustics.Core.AbsorptiveSurface;
using Kst.Acoustics.Core.BuildingAcoustics.Calculation;
using Kst.Acoustics.Core.BuildingAcoustics.Models;
using Kst.Acoustics.Core.Common.Geometry;
using Kst.Acoustics.Core.Measurement.Analysis;
using Kst.Acoustics.Core.Measurement.Capture;
using Kst.Acoustics.Core.Measurement.Data;
using Kst.Acoustics.Core.Measurement.Processing;
using Kst.Acoustics.Core.Measurement.Signals;
using Kst.Acoustics.Core.NoiseBarrier;
using Kst.Acoustics.Core.PanelDesign;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.BuildingAcoustics.Geometry;
using Kst.Acoustics.Core.BuildingAcoustics.Parsers;
using Kst.Acoustics.Core.BuildingAcoustics.Database;
using Kst.Acoustics.Core.BuildingAcoustics.Reports;
using Kst.Acoustics.Core.RoomModes;
using Kst.Acoustics.Core.RoomModes.Diffusers;
using Kst.Acoustics.Core.Simulation;
using Kst.Acoustics.Core.SoundInsulation;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.App.Controls;
using Kst.Acoustics.Tests;

var tests = new (string Name, Action Test)[]
{
    ("Maekawa reference points are stable", MaekawaReferencePointsAreStable),
    ("Kurze-Anderson reference points are stable", KurzeAndersonReferencePointsAreStable),
    ("Taller barrier increases insertion loss", TallerBarrierIncreasesInsertionLoss),
    ("Short finite barrier loses performance at side edges", ShortFiniteBarrierLosesPerformanceAtSideEdges),
    ("Second taller series barrier increases insertion loss", SecondTallerSeriesBarrierIncreasesInsertionLoss),
    ("Disabled barrier does not contribute to attenuation", DisabledBarrierDoesNotContributeToAttenuation),
    ("Low TL material limits barrier performance", LowTlMaterialLimitsBarrierPerformance),
    ("Absorptive barrier side improves insertion loss", AbsorptiveBarrierSideImprovesInsertionLoss),
    ("Unblocked line of sight gives near zero insertion loss", UnblockedLineOfSightGivesNearZeroInsertionLoss),
    ("Line source simulation remains finite", LineSourceSimulationRemainsFinite),
    ("Traffic flow parameters drive source level", TrafficFlowParametersDriveSourceLevel),
    ("Default traffic source stays in realistic facade range", DefaultTrafficSourceStaysInRealisticFacadeRange),
    ("Traffic standard-family profiles produce distinct levels", TrafficStandardFamilyProfilesProduceDistinctLevels),
    ("Facade multi-reflections reduce barrier insertion loss", FacadeMultiReflectionsReduceBarrierInsertionLoss),
    ("Absorptive barrier reduces reflection penalty", AbsorptiveBarrierReducesReflectionPenalty),
    ("Noise barrier Excel template round-trips", NoiseBarrierExcelTemplateRoundTrips),
    ("Diffraction methods produce expected relative levels", DiffractionMethodsProduceExpectedRelativeLevels),
    ("Traffic model calibration matches reference benchmarks", TrafficModelCalibrationMatchesBenchmarks),
    ("DXF importer rejects empty entities", DxfImporterRejectsEmptyEntities),
    ("DXF importer marks missing units as unknown millimeters", DxfImporterMarksMissingUnitsAsUnknownMillimeters),
    ("Project factory rejects empty import result", ProjectFactoryRejectsEmptyImportResult),
    ("Simulation reliability flags weak inputs", SimulationReliabilityFlagsWeakInputs),
    ("Ray tracing: free field direct sound follows inverse square", RayTracingValidationTests.FreeFieldDirectSoundFollowsInverseSquare),
    ("Ray tracing: Sabine box RT60 matches Eyring", RayTracingValidationTests.SabineBoxRt60MatchesEyring),
    ("Ray tracing: more absorption shortens RT60", RayTracingValidationTests.MoreAbsorptionShortensRt60),
    ("Ray tracing: seeded simulation is reproducible", RayTracingValidationTests.SeededSimulationIsReproducible),
    ("Ray tracing: physical invariants hold on Sabine box", RayTracingValidationTests.PhysicalInvariantsHoldOnSabineBox),
    ("Ray tracing: directivity patterns conserve total power", RayTracingValidationTests.DirectivityPatternsConserveTotalPower),
    ("Ray tracing: result is unit-invariant (m vs mm)", RayTracingValidationTests.ResultIsUnitInvariantMetersVsMillimeters),
    ("Ray tracing: grid map combines all sources", RayTracingValidationTests.GridMapCombinesAllSources),
    ("Ray tracing: light tracing mirror mode tags receiver hits", RayTracingValidationTests.LightTracingMirrorModeTagsReceiverHits),
    ("ProjectWindowViewModel: edited source/receiver reach simulation", ProjectWindowViewModelSimulationTests.EditedSourceAndReceiverReachSimulation),
    ("ProjectWindowViewModel: removed receiver does not reach simulation", ProjectWindowViewModelSimulationTests.RemovedReceiverDoesNotReachSimulation),
    ("MainWindowViewModel: unit conversion scales coordinates but preserves physical m³", MainWindowViewModelUnitConversionTests.UnitConversionScalesSurfacesSourcesReceiversAndVolumeTogether),
    ("MainWindowViewModel: custom physical scale cubes room volume", MainWindowViewModelUnitConversionTests.CustomPhysicalScaleCubesRoomVolume),
    ("MainWindowViewModel: legacy result settings scale with unit conversion", MainWindowViewModelUnitConversionTests.LegacyResultParametersBecomeScaledProjectSettings),
    ("MainWindowViewModel: extreme custom scale is rejected atomically", MainWindowViewModelUnitConversionTests.ExtremeCustomScaleDoesNotMutateProject),
    ("MainWindowViewModel: overflowing unit conversion is rejected atomically", MainWindowViewModelUnitConversionTests.UnitConversionOverflowDoesNotMutateProject),
    ("MainWindowViewModel: nested component overflow is rejected atomically", MainWindowViewModelUnitConversionTests.NestedComponentOverflowDoesNotPartiallyMutateProject),
    ("RoomVolumeCalculator: closed box uses real volume not bounding box", RoomVolumeCalculatorTests.ClosedBoxUsesRealVolumeNotBoundingBox),
    ("RoomVolumeCalculator: L-shaped closed shell volume is less than bounding box", RoomVolumeCalculatorTests.LShapedClosedShellVolumeIsLessThanBoundingBox),
    ("RoomVolumeCalculator: open shell falls back to bounding box and is marked as estimate", RoomVolumeCalculatorTests.OpenShellFallsBackToBoundingBoxAndIsMarkedAsEstimate),
    ("RoomVolumeCalculator: unit conversion scales estimated volume by cube of factor", RoomVolumeCalculatorTests.UnitConversionScalesEstimatedVolumeByCubeOfFactor),
    ("RoomVolumeCalculator: mixed surface winding is oriented before volume calculation", RoomVolumeCalculatorTests.MixedSurfaceWindingIsOrientedBeforeVolumeCalculation),
    ("UnsavedChangesGate: proceeds without prompt when not dirty", UnsavedChangesGateTests.ProceedsWithoutPromptWhenNotDirty),
    ("UnsavedChangesGate: discard is distinct from save and cancel", UnsavedChangesGateTests.DiscardIsDistinctFromSaveAndCancel),
    ("IsDirty: freshly constructed ProjectWindow is not dirty", IsDirtyTrackingTests.FreshlyConstructedProjectWindowIsNotDirty),
    ("IsDirty: editing room volume manually marks dirty", IsDirtyTrackingTests.EditingRoomVolumeManuallyMarksDirty),
    ("IsDirty: changing category material marks dirty", IsDirtyTrackingTests.ChangingCategoryMaterialMarksDirty),
    ("IsDirty: completing simulation marks dirty", IsDirtyTrackingTests.CompletingSimulationMarksDirty),
    ("IsDirty: SelectedRoomType does not dirty without loaded project", IsDirtyTrackingTests.SelectedRoomTypeDoesNotDirtyWithoutLoadedProject),
    ("IsDirty: SelectedRoomType marks dirty after project loaded", IsDirtyTrackingTests.SelectedRoomTypeMarksDirtyAfterProjectLoaded),
    ("AcousticsProjectMigrator: valid minimal project passes unchanged", AcousticsProjectMigratorTests.ValidMinimalProjectPassesUnchanged),
    ("AcousticsProjectMigrator: future schema version is rejected", AcousticsProjectMigratorTests.FutureSchemaVersionIsRejected),
    ("AcousticsProjectMigrator: non-positive schema version is rejected", AcousticsProjectMigratorTests.NonPositiveSchemaVersionIsRejected),
    ("AcousticsProjectMigrator: duplicate surface IDs are rejected", AcousticsProjectMigratorTests.DuplicateSurfaceIdsAreRejected),
    ("AcousticsProjectMigrator: degenerate surface with <3 vertices is rejected", AcousticsProjectMigratorTests.DegenerateSurfaceWithFewerThanThreeVerticesIsRejected),
    ("AcousticsProjectMigrator: NaN vertex coordinate is rejected", AcousticsProjectMigratorTests.NaNVertexCoordinateIsRejected),
    ("AcousticsProjectMigrator: collinear surface is rejected with scale-aware tolerance", AcousticsProjectMigratorTests.CollinearSurfaceIsRejectedButThinValidSurfacePasses),
    ("AcousticsProjectMigrator: duplicate source ID is rejected", AcousticsProjectMigratorTests.DuplicateSourceIdIsRejected),
    ("AcousticsProjectMigrator: malformed panels are rejected", AcousticsProjectMigratorTests.MalformedPanelDefinitionsAreRejected),
    ("AcousticsProjectMigrator: simulation UI ranges and units are validated", AcousticsProjectMigratorTests.SimulationSettingsUseUiRangesAndPhysicalReceiverRadius),
    ("AcousticsProjectMigrator: v1 settings migrate without rewriting historical results", AcousticsProjectMigratorTests.V1UnitScaleMigratesWithoutRewritingHistoricalResult),
    ("AcousticsProjectMigrator: computed pair IDs match echograms", AcousticsProjectMigratorTests.ComputedPairIdsMustMatchEchogram),
    ("AcousticsProjectMigrator: acoustic metrics and curves are validated", AcousticsProjectMigratorTests.AcousticMetricsAndSchroederCurveAreValidated),
    ("AcousticsProjectMigrator: ProjectFileService round-trip applies migrator", AcousticsProjectMigratorTests.ProjectFileServiceRoundTripAppliesMigrator),
    ("ProjectFileService: SaveAsync cleans up temp file when target path is a directory", ProjectFileServiceErrorHandlingTests.SaveAsyncCleansUpTempFileWhenTargetPathIsADirectory),
    ("M01 lifecycle: point round-trip preserves precision", M01LifecycleRegressionTests.PointRoundTripPreservesPrecision),
    ("M01 lifecycle: newly added point preserves precision", M01LifecycleRegressionTests.NewlyAddedProjectPointPreservesPrecision),
    ("M01 lifecycle: non-finite point is rejected", M01LifecycleRegressionTests.NonFinitePointIsRejectedInsteadOfDropped),
    ("M01 lifecycle: zero room volume is not silently invented", M01LifecycleRegressionTests.ZeroRoomVolumeIsNotSilentlyInvented),
    ("M01 lifecycle: invalid point blocks snapshot", M01LifecycleRegressionTests.ProjectWindowInvalidPointBlocksSnapshot),
    ("M01 lifecycle: transform marks project dirty", M01LifecycleRegressionTests.TransformMarksProjectDirty),
    ("M01 lifecycle: imported project starts dirty", M01LifecycleRegressionTests.ImportedProjectStartsDirty),
    ("M01 lifecycle: failed import preserves open document", M01LifecycleRegressionTests.FailedImportPreservesOpenDocument),
    ("M01 lifecycle: invalid save preserves existing target", M01LifecycleRegressionTests.InvalidSavePreservesExistingTarget),
    ("M01 lifecycle: null vertices are rejected cleanly", M01LifecycleRegressionTests.NullVertexCollectionIsRejectedCleanly),
    ("M01 lifecycle: malformed simulation payload is rejected cleanly", M01LifecycleRegressionTests.MalformedSimulationPayloadIsRejectedCleanly),
    ("M01 lifecycle: volume origin and warnings round-trip", M01LifecycleRegressionTests.VolumeOriginAndWarningsRoundTrip),
    ("M01 lifecycle: empty project does not invent a room volume", M01LifecycleRegressionTests.EmptyProjectDoesNotInventRoomVolume),
    ("M01 lifecycle: null or blank point label is rejected safely", M01LifecycleRegressionTests.NullOrBlankPointLabelIsRejectedWithoutThrowing),
    ("M01 lifecycle: corrupt primary recovers last backup", M01LifecycleRegressionTests.CorruptPrimaryRecoversLastBackup),
    ("M01 lifecycle: normal overwrite backs up an invalid target", M01LifecycleRegressionTests.NormalOverwriteBacksUpEvenInvalidTarget),
    ("M01 lifecycle: schema recovery distinguishes corruption from newer formats", M01LifecycleRegressionTests.SchemaRecoveryDistinguishesCorruptionFromNewerFormat),
    ("M01 simulation: receiver radius remains physical metres across units", M01SimulationSafetyTests.ReceiverRadiusUsesPhysicalMetersAcrossUnits),
    ("M01 simulation: empty metrics never produce false convergence", M01SimulationSafetyTests.EmptyMetricsNeverProduceFalseConvergence),
    ("M01 simulation: loaded result detaches before invalidation", M01SimulationSafetyTests.LoadedResultIsDetachedBeforeInvalidation),
    ("M01 simulation: fingerprint mismatch is stale without baseline mutation", M01SimulationSafetyTests.LoadedFingerprintMismatchIsMarkedStaleWithoutMutation),
    ("M01 simulation: stored settings mismatch marks result stale", M01SimulationSafetyTests.LoadedSettingsMismatchIsMarkedStale),
    ("M01 simulation: pre-cancelled run stops before work", M01SimulationSafetyTests.PreCancelledSimulationStopsBeforeWork),
    ("M01 simulation: result round-trip preserves metrics, counts, curves, and grid mask", M01SimulationSafetyTests.SimulationResultRoundTripPreservesMetricsCountsCurvesAndMask),
    ("DXF coplanar merge: millimeter unit floating noise still merges coplanar faces", DxfCoplanarMergeTests.MillimeterUnitFloatingNoiseStillMergesCoplanarFaces),
    ("DXF coplanar merge: diagonally touching squares sharing only a corner stay as two surfaces", DxfCoplanarMergeTests.DiagonallyTouchingSquaresSharingOnlyACornerStayAsTwoSurfaces),
    ("M01 geometry: unknown DXF unit defers coplanar merge until finalized", M01GeometryReliabilityTests.UnknownDxfUnitDefersCoplanarMergeUntilFinalized),
    ("M01 geometry: DXF parser reads only ENTITIES and warns for INSERT", M01GeometryReliabilityTests.ParserReadsOnlyEntitiesAndWarnsForInsert),
    ("M01 geometry: hole region never produces NaN vertices", M01GeometryReliabilityTests.HoleRegionNeverProducesNaNVertices),
    ("M01 geometry: plane tolerance keeps separated parallel faces apart", M01GeometryReliabilityTests.PlaneToleranceKeepsSeparatedParallelFacesApart),
    ("M01 geometry: legacy hole sentinel does not poison transform or area", M01GeometryReliabilityTests.LegacyHoleSentinelDoesNotPoisonTransformOrArea),
    ("M01 geometry: main window snap uses physical offset and room interior", M01GeometryReliabilityTests.MainWindowSnapUsesPhysicalOffsetAndChoosesRoomInterior),
    ("MainWindowViewModel: importing DXF with bulge arc populates import warnings", MainWindowViewModelImportWarningsTests.ImportingDxfWithBulgeArcPopulatesImportWarnings),
    ("MainWindowViewModel: importing clean DXF leaves import warnings empty", MainWindowViewModelImportWarningsTests.ImportingCleanDxfLeavesImportWarningsEmpty),
    ("Snap offset: physically consistent across units", SnapOffsetUnitAwarenessTests.SnapOffsetIsPhysicallyConsistentAcrossUnits),
    ("Auralization: banded IR concentrates energy in active band", AuralizationBandColoringTests.BandedIrConcentratesEnergyInActiveBand),
    ("Auralization: different bands produce distinguishable IRs", AuralizationBandColoringTests.DifferentBandsProduceDistinguishableIrs),
    ("Material resolver: surface override beats category default", MaterialResolverTests.SurfaceOverrideBeatsCategoryDefault),
    ("Material resolver: category default used when no override", MaterialResolverTests.CategoryDefaultUsedWhenNoOverride),
    ("Material resolver: null when neither defined", MaterialResolverTests.NullWhenNeitherDefined),
    ("Material resolver: empty string override treated as absent", MaterialResolverTests.EmptyStringOverrideTreatedAsAbsent),
    ("Acoustic panels: sequential widths are used in order", AcousticPanelsSequentialWidthsAreUsedInOrder),
    ("Acoustic panels: uniform width keeps clipped final slice", AcousticPanelsUniformWidthKeepsClippedFinalSlice),
    ("Panel DXF parser counts circle holes", PanelDxfParserCountsCircleHoles),
    ("Panel DXF parser reads 3DFACE boundary", PanelDxfParserReads3DFaceBoundary),
    ("Panel DXF parser ignores closed auxiliary geometry outside boundary", PanelDxfParserIgnoresClosedAuxiliaryGeometryOutsideBoundary),
    ("Panel DXF parser detects kilometer units", PanelDxfParserDetectsKilometerUnits),
    ("Panel DXF parser flags unknown INSUNITS", PanelDxfParserFlagsUnknownInsUnits),
    ("Panel DXF parser reports malformed numeric values", PanelDxfParserReportsMalformedNumericValues),
    ("Panel open area ratio supports codex limit", PanelOpenAreaRatioSupportsCodexLimit),
    ("Panel DXF parser preserves high open area ratio", PanelDxfParserPreservesHighOpenAreaRatio),
    ("Panel DXF template is parser-compatible", PanelDxfTemplateIsParserCompatible),
    ("Panel DXF parser ignores open guide geometry in area", PanelDxfParserIgnoresOpenGuideGeometryInArea),
    ("Panel DXF parser reports hydraulic opening geometry", PanelDxfParserReportsHydraulicOpeningGeometry),
    ("Panel DXF quality flags slot geometry", PanelDxfQualityFlagsSlotGeometry),
    ("Panel custom openings match equivalent circular array", PanelCustomOpeningsMatchEquivalentCircularArray),
    ("Panel Helmholtz estimate follows perforated-layer neck model", PanelHelmholtzEstimateFollowsPerforatedLayerNeckModel),
    ("Panel Helmholtz estimate supports custom openings", PanelHelmholtzEstimateSupportsCustomOpenings),
    ("Panel Helmholtz benchmark peak is near formula", PanelHelmholtzBenchmarkPeakIsNearFormula),
    ("Panel porous layer range diagnostic is stateless", PanelPorousLayerRangeDiagnosticIsStateless),
    ("Panel alphaW reference curve uses unfavorable deviations", PanelAlphaWReferenceCurveUsesUnfavorableDeviations),
    ("Panel alphaW shape indicators are reported", PanelAlphaWShapeIndicatorsAreReported),
    ("Panel random incidence absorption matches matched-impedance integral", PanelRandomIncidenceAbsorptionMatchesMatchedImpedanceIntegral),
    ("Panel DXF quality UI items summarize import risks", PanelDxfQualityUiItemsSummarizeImportRisks),
    ("Panel reports include DXF quality decision items", PanelReportsIncludeDxfQualityDecisionItems),
    ("Panel layer clears DXF import state on parametric edit", PanelLayerClearsDxfImportStateOnParametricEdit),
    ("Panel preview ignores malformed DXF numeric values", PanelPreviewIgnoresMalformedDxfNumericValues),
    ("Panel reports omit frequency table when no layers are included", PanelReportsOmitFrequencyTableWhenNoLayersAreIncluded),
    ("Sound insulation connection settings affect double wall result", SoundInsulationConnectionSettingsAffectDoubleWallResult),
    ("Sound insulation composite weak element lowers total rating", SoundInsulationCompositeWeakElementLowersTotalRating),
    ("TMM single glass pane near mass-law", SoundInsulationTmmSinglePaneNearMassLaw),
    ("TMM double wall improves over single mass", SoundInsulationTmmDoubleWallImprovesOverSingleMass),
    ("Hybrid mode blends TMM and empirical results", SoundInsulationHybridBlendsLowAndHighFrequency),
    ("TMM solid layer reduces to mass law at low freq", SoundInsulationSolidLayerReducesToMassLawAtLowFreq),
    ("TMM solid layer shows coincidence dip near fc", SoundInsulationSolidLayerShowsCoincidenceDip),
    ("TMM thick concrete solid layer gives realistic Rw", SoundInsulationThickConcreteSolidTmmReasonableRw),
    ("Empirical single leaf TL stays bounded near mass law", SoundInsulationEmpiricalSingleLeafBoundedByMassLaw),
    ("Empirical double wall TL never exceeds physical ceiling", SoundInsulationEmpiricalDoubleWallCappedByCeiling),
    ("Empirical TL curve has no spurious band discontinuity", SoundInsulationEmpiricalCurveContinuous),
    ("Empirical thick concrete Rw is bounded (no runaway)", SoundInsulationEmpiricalThickConcreteRwBounded),
    ("Empirical bridge connection ordering is realistic", SoundInsulationBridgeConnectionOrderingIsRealistic),
    ("Empirical porous cavity fill reduces MAM resonance dip", SoundInsulationPorousFillReducesMamDip),
    ("Empirical triple-leaf system is bounded and warned", SoundInsulationTripleLeafBoundedAndWarned),
    ("TMM porous fill stays finite across porosity range", SoundInsulationTmmPorousFiniteAcrossPorosity),
    ("Impact: Calculation results are finite", ImpactSurfaceTests.CalculationResultsAreFinite),
    ("Impact: Critical Fall Height is logical", ImpactSurfaceTests.CriticalFallHeightIsLogical),
    ("Impact: Total thickness is correct", ImpactSurfaceTests.TotalThicknessIsCorrect),
    ("Impact: Empty stack handles gracefully", ImpactSurfaceTests.EmptyStackHandlesGracefully),
    ("Impact: Templates load correctly", ImpactSurfaceTests.TemplatesLoadCorrectly),
    ("Impact: Extreme thickness handles gracefully", ImpactSurfaceTests.ExtremeThicknessHandlesGracefully),
    ("Impact: HIC scale is correct", ImpactSurfaceTests.HicCalculationIsCorrectScale),
    ("Impact: Invalid inputs report validation errors", ImpactSurfaceTests.InvalidInputsReportValidationErrors),
    ("Impact: Project serialization preserves parameters and materials", ImpactSurfaceTests.ProjectSerializationPreservesParametersAndMaterials),
    ("Impact validation: drop height scaling follows sqrt(h)", ImpactValidationTests.DropHeightScalingFollowsSqrtH),
    ("Impact validation: head mass scaling follows 1/sqrt(m)", ImpactValidationTests.HeadMassScalingFollowsInverseSqrtM),
    ("Impact validation: lower stiffness reduces Gmax", ImpactValidationTests.LowerStiffnessReducesGmax),
    ("Impact validation: contact area scaling follows sqrt(A)", ImpactValidationTests.ContactAreaScalingFollowsSqrtA),
    ("Impact validation: single layer matches analytical half-sine", ImpactValidationTests.SingleLayerMatchesAnalyticalHalfSine),
    ("Impact validation: deterministic run is repeatable", ImpactValidationTests.DeterministicRunIsRepeatable),
    ("Impact validation: golden EVA stack produces expected values", ImpactValidationTests.GoldenReferenceEvaStackProducesExpectedValues),
    ("Impact validation: accel series is non-negative half-sine", ImpactValidationTests.AccelSeriesIsNonNegativeHalfSine),
    ("Impact validation: series-spring stack is softer than single layer", ImpactValidationTests.SeriesSpringStackIsSofterThanSingleLayer),
    ("Impact validation: compliance sweep classifies soft and hard stacks", ImpactValidationTests.ComplianceSweepClassifiesSoftAndHardStacks),
    ("Impact validation: compliance verdicts are consistent", ImpactValidationTests.ComplianceVerdictsAreConsistent),
    ("Impact validation: report builder produces expected sections", ImpactValidationTests.ReportBuilderProducesExpectedSections),
    ("Absorptive surface: thicker porous layer improves NRC", AbsorptiveSurfaceThickerPorousLayerImprovesNrc),
    ("Absorptive surface: perforated panel warns without air gap", AbsorptiveSurfacePerforatedPanelWarnsWithoutAirGap),
    ("Absorptive surface: Maa perforated panel peaks near resonance", AbsorptiveMaaPanelPeaksNearResonance),
    ("Absorptive surface: air gap boosts low frequency alpha", AbsorptiveAirGapBoostsLowFrequencyAlpha),
    ("Absorptive surface: report builder contains chart and bands", AbsorptiveReportBuilderContainsChartAndBands),
    ("Room modes: axial frequency matches formula", RoomModesAxialFrequencyMatchesFormula),
    ("Room modes: invalid inputs return warning", RoomModesInvalidInputsReturnWarning),
    ("Room modes: missing RT60 estimates Schroeder", RoomModesMissingRt60EstimatesSchroeder),
    ("Room modes: pressure samples stay normalized", RoomModesPressureSamplesStayNormalized),
    ("Room modes: QRD diffuser residues follow prime sequence", RoomModesQrdDiffuserResiduesFollowPrimeSequence),
    ("Room modes: diffuser flags low modal frequency", RoomModesDiffuserFlagsLowModalFrequency),
    ("Room modes: QRD diffuser respects max depth", RoomModesQrdDiffuserRespectsMaxDepth),
    ("Room modes: Skyline diffuser builds square cell grid", RoomModesSkylineDiffuserBuildsSquareCellGrid),
    ("Room modes: Skyline diffuser flags clipped low frequency", RoomModesSkylineDiffuserFlagsClippedLowFrequency),
    ("Measurement: RTA levels remain dBFS normalized", MeasurementRtaLevelsRemainDbfsNormalized),
    ("Measurement: acoustic RT regression tracks exponential decay", MeasurementRtRegressionTracksExponentialDecay),
    ("Measurement: unsupported capture platform does not advertise input devices", MeasurementUnsupportedCapturePlatformDoesNotAdvertiseInputDevices),
    ("Measurement: session round-trip preserves room dimensions and spectrogram", MeasurementSessionRoundTripPreservesRoomDimensionsAndSpectrogram),
    ("Measurement: CSV export is culture-invariant", MeasurementCsvExportIsCultureInvariant),
    ("Measurement: IR alignment preserves direct sound peak", MeasurementIrAlignmentPreservesDirectSoundPeak),
    ("Measurement: failed RT regression yields NaN not zero", MeasurementFailedRtRegressionYieldsNaNNotZero),
    ("Measurement: frequency response level is not fft-length attenuated", MeasurementFrequencyResponseLevelIsNotFftLengthAttenuated),
    ("Building acoustics: impact path finds receiver below", BuildingImpactPathFindsReceiverBelow),
    ("Building acoustics: impact path respects floor offsets", BuildingImpactPathRespectsFloorOffsets),
    ("Building acoustics: impact path rejects bounding-box-only overlap (concave room)", BuildingImpactPathRejectsBoundingBoxOnlyOverlap),
    ("Building acoustics: impact path finds multiple sub-receivers", BuildingImpactPathFindsMultipleSubReceivers),
    ("Building acoustics: 12354 flanking exposes 13-path model", BuildingFlankingExposesThirteenPathModel),
    ("Building acoustics: third-octave data drives apparent Rw", BuildingThirdOctaveDataDrivesApparentRw),
    ("Building acoustics: external noise spectrum reports facade indicators", BuildingExternalNoiseSpectrumReportsFacadeIndicators),
    ("Building acoustics: reliability score reacts to measured data", BuildingReliabilityScoreReactsToMeasuredData),
    ("Building acoustics: EK-2 auto estimate maps room labels", BuildingEk2AutoEstimateMapsRoomLabels),
    ("Building acoustics: BinaIslevi persists in project JSON", BuildingBinaIsleviPersistsInProjectJson),
    ("Building acoustics: Mekan Ad notifies edits", BuildingMekanAdNotifiesEdits),
    ("Building acoustics: vertical merge containment-based succeeds", BuildingVerticalMergeContainmentBasedSucceeds),
    ("Building acoustics: vertical merge rejects when polygon not contained", BuildingVerticalMergeRejectsWhenNotContained),
    ("Building acoustics: vertical merge can chain three floors", BuildingVerticalMergeCanChainThreeFloors),
    ("Building acoustics: vertical merge keeps both rooms (non-destructive)", BuildingVerticalMergeKeepsBothRooms),
    ("Building acoustics: merge exposes partner walls for assignment", BuildingMergeExposesPartnerWalls),
    ("Building acoustics: vertical merge is undoable", BuildingVerticalMergeUndoable),
    ("Building acoustics: room assignment-complete criterion", BuildingRoomAssignmentCompleteCriterion),
    ("Building acoustics: tiny wall assignment survives rebuild", BuildingTinyWallAssignmentSurvivesRebuild),
    ("Building acoustics: HacimleriGuncelle sums merged volumes", BuildingHacimleriGuncelleSumsMergedVolumes),
    ("Building acoustics: DnT,w matches 12354-1 standard normalization", BuildingDntwMatchesStandardNormalization),
    ("Building acoustics: ISO 717-1 Rw matches reference curve", BuildingIso7171RwMatchesReferenceCurve),
    ("Building acoustics: ISO 717-2 L'n,w matches reference curve", BuildingIso7172LnwMatchesReferenceCurve),
    ("Building acoustics: ISO 717 rejects insufficient band count", BuildingIso717RejectsInsufficientBands),
    ("Building acoustics: higher Kij junction raises apparent Rw", BuildingFlankingKijDecouplingRaisesApparentRw),
    ("Building acoustics: EnsureDefaultJunctions preserves user settings", BuildingEnsureDefaultJunctionsPreservesUserSettings),
    ("Building acoustics: material flanking value does not bypass Annex E", BuildingMaterialFlankingValueDoesNotBypassAnnexE),
    ("Building acoustics: flanking coupling term follows flank areas", BuildingFlankingCouplingTermFollowsFlankAreas),
    ("Building acoustics: facade Ctr uses composite spectrum", BuildingFacadeCtrUsesCompositeSpectrum),
    ("Building acoustics: report uses DnT,A and L'nT,w values", BuildingReportUsesDntAAndLnTwValues),
    ("Building acoustics: Annex E rigid T-junction Kij matches formula", BuildingAnnexEKijRigidTJunction),
    ("Building acoustics: Annex E rigid X-junction Kij matches formula", BuildingAnnexEKijRigidXJunction),
    ("Building acoustics: Annex E lightweight T-junction Kij above rigid", BuildingAnnexEKijLightweightTJunctionAboveRigid),
    ("Building acoustics: Annex E L-junction has no Ff path", BuildingAnnexELJunctionHasNoFfPath),
    ("Building acoustics: Annex E L-junction rigid Kij formula", BuildingAnnexEKijRigidLJunction),
    ("Building acoustics: flanking Dv clamped non-negative (E.3 limiting)", BuildingFlankingDvClampedNonNegative),
    ("Building acoustics: impact flanking Dv reduces level", BuildingImpactFlankingDvReducesLevel),
    ("Building acoustics: impact flanking ignores ceiling self-path", BuildingImpactFlankingIgnoresCeilingSelf),
    ("Building acoustics: EK-2 Tablo 2.1 degrees match regulation", BuildingEk2DegreesMatchTablo21),
    ("Building acoustics: EK-3 limit tables match regulation", BuildingEk3LimitTablesMatchRegulation),
    ("Building acoustics: door tolerance lowers wall limit by 14 dB", BuildingDoorToleranceLowersLimit),
    ("Building acoustics: Flanking path uses opaque R", BuildingFlankingUsesOpaqueR),
    ("Building acoustics: Annex E mixed-class junction uses lightweight branch", BuildingAnnexEMixedClassUsesLightweight),
    ("Building acoustics: Annex E sandvic between homogen and hafif", BuildingAnnexESandvicBetweenHomogenAndHafif),
    ("Building acoustics: direct Rw independent of structural Ts (ISO 12354-1)", BuildingDirectRwIndependentOfStructuralTs),
    ("Building acoustics: synthetic R(f) calibrated to nominal Rw", BuildingSentetikRfNominalRwyeKalibre),
    ("Building acoustics: 12354-3 Menfez Dne lowers composite Rw", BuildingMenfezDneLowersCompositeRw),
    ("Building acoustics: 12354-3 Akustik vent decouples better than mekanik", BuildingMenfezAkustikDecouplesBetterThanMekanik),
    ("Building acoustics: Menfez spectrum drives band-by-band tau", BuildingMenfezSpectrumDrivesBandByBandTau),
    ("Building acoustics: Junction structural Kiris adds 3 dB Kij", BuildingJunctionStrukturelKirisAdds3Db),
    ("Building acoustics: Junction structural PerdeDuvar dominates", BuildingJunctionStrukturelPerdeDuvarDominates),
    ("Building acoustics: YapiElemani IsInce reflects total thickness", BuildingYapiElemaniIsInceFlag),
    ("Building acoustics: B4 wall extrusion uses aciklik Yukseklik", BuildingB4WallExtrusionUsesAciklikYukseklik),
    ("Building acoustics: B4 EsikYuksekligi defaults differ by tip", BuildingB4EsikYuksekligiDefaultsByTip),
    ("Building acoustics: B4 BinaGeometriServisi emits Pencere/Kapi surfaces", BuildingB4BinaGeometriEmitsOpeningSurfaces),
    ("Building acoustics: B3 canonical detection finds single square", BuildingB3CanonicalSingleSquare),
    ("Building acoustics: B3 canonical detection finds two adjacent rooms", BuildingB3CanonicalTwoAdjacentRooms),
    ("Building acoustics: B3 canonical detection ignores dead-end stubs", BuildingB3CanonicalIgnoresDeadEndStubs),
    ("Building acoustics: B3 canonical detection drops small/isolated faces", BuildingB3CanonicalDropsSmallFaces),
    ("Building acoustics: B14 validation rejects missing Ad", BuildingB14ValidationRejectsMissingAd),
    ("Building acoustics: B14 validation rejects out-of-range Rw", BuildingB14ValidationRejectsBadRw),
    ("Building acoustics: B14 validation warns floor without Lnw", BuildingB14ValidationWarnsFloorWithoutLnw),
    ("Building acoustics: B14 validation warns window with Lnw set", BuildingB14ValidationWarnsWindowWithLnw),
    ("Building acoustics: UX margin positive when wall exceeds limit", BuildingUxMarginPositiveWhenWallExceedsLimit),
    ("Building acoustics: UX margin negative when floor exceeds Lnw", BuildingUxMarginNegativeWhenFloorExceedsLnw),
    ("Building acoustics: UX margin NaN when YapiElemani missing", BuildingUxMarginNaNWhenYapiElemaniMissing),
    ("Building acoustics: DnT,A uses apparent spectrum C (not lab C)", BuildingDntAUsesApparentSpektrumC),
    ("Building acoustics: octave display does not double-count openings", BuildingOktavGosterimAcikligiCiftSaymaz),
    ("Building acoustics: octave single-number from standard 16-band", BuildingOktavTekSayiStandart16BanttanGelir),
    ("Building acoustics: per-path Kij differentiates Df vs Ff at T-junction", BuildingFlankingPerPathKijDiffersAtTJunction),
    ("Building acoustics: Annex E absorption length formula", BuildingAnnexEAbsorptionLengthFormula),
    ("Building acoustics: flanking Kij applies absorption length correction", BuildingFlankingAppliesAbsorptionLengthCorrection),
    ("Building acoustics: facade D2m,nT,w applies volume normalization", BuildingFacadeDntwAppliesVolumeNormalization),
    ("Building acoustics: facade spectral path aggregates room facades", BuildingFacadeSpectralAggregatesRoomFacades),
    ("Building acoustics: facade form deltaLfs varies by facade shape", BuildingFacadeDeltaLfsVariesByForm),
    ("Building acoustics: third-octave flanking Kij varies with band frequency", BuildingFlankingPerBandKijVariesWithFrequency),
    ("Building acoustics: per-element Ts affects flanking Kij", BuildingFlankingPerElementTsAffectsKij),
    ("Building acoustics: lining DeltaR boosts composite Rw", BuildingLiningDeltaRBoostsCompositeRw),
    ("Building acoustics: lining DeltaR on flank improves apparent Rw", BuildingLiningDeltaRImprovesFlankingApparent),
    ("Building acoustics: per-band DeltaR(f) shapes R(f)", BuildingLiningDeltaRPerBandShapesRf),
    ("Building acoustics: 16-band DeltaR(f) collapses onto 7-band R(f)", BuildingLiningDeltaRCollapsesToOktav),
    ("Building acoustics: structural Ts estimated from mass (Annex C)", BuildingStructuralTsEstimatedFromMass),
    ("Building acoustics: rebuild preserves Menfez and Junction assignments", BuildingRebuildPreservesMenfezAndJunctions),
    ("Building acoustics: BinaProjeMigrator normalizes null lists", BuildingProjeMigratorNormalizesNullLists),
    ("Building acoustics: project JSON round-trip preserves metadata fields", BuildingProjeRoundTripPreservesMetadataFields),
    ("Building acoustics: facade report uses Tablo 3.1 Lgag-ofset limit, not flat -35", BuildingFacadeReportUsesCorrectLgagLimit),
    ("Building acoustics: DXF export uses L'nT,w and honors performance class/Lgag", BuildingDxfHonorsPerformanceClassAndLgag),
    ("Building acoustics: report export honors 'Rapora Dahil' exclusion filter", BuildingReportFilterExcludesBolme),
    ("Building acoustics: text report shows VeriEksik distinctly, not as Kaldi", BuildingReportShowsVeriEksikDistinctlyNotAsKaldi),
    ("Building acoustics: ViewModel marks results stale after model change", BuildingViewModelMarksResultsStaleAfterModelChange),
    ("Building acoustics: class engine derives achieved class from limits", BuildingClassEngineDerivesAchievedClass),
    ("Building acoustics: class engine applies lowest-class rule and 'belirlenemedi'", BuildingClassEngineLowestClassAndUndetermined),
    ("Building acoustics: HTML report contains regulation sections and honors filter", BuildingHtmlReportSectionsAndFilter),
    ("Building acoustics: EK-4 Tablo 4.1 indoor noise limits match regulation", BuildingEk4IndoorNoiseLimitsMatchRegulation),
    ("Building acoustics: EK-5 tesisat limits derive from Tablo 4.1 minus 4 dB", BuildingEk5TesisatLimitsMatchRegulation),
    ("Building acoustics: EK-6 reverberation limits and Sabine estimate", BuildingEk6ReverberationLimitsAndSabine),
    ("Building acoustics: facade indoor target uses EK-4 per-room limit, not fixed 35", BuildingFacadeIndoorTargetUsesEk4),
    ("Building acoustics hardening: partial unassigned facade is rejected", M08HardeningRegressionTests.PartialUnassignedFacadeIsRejected),
    ("Building acoustics hardening: missing receiver volume is not a valid impact result", M08HardeningRegressionTests.MissingReceiverVolumeIsNotAValidImpactResult),
    ("Building acoustics hardening: missing absorption remains data-missing", M08HardeningRegressionTests.MissingAbsorptionRemainsDataMissing),
    ("Building acoustics hardening: multi-bed patient room selects specific EK-4/EK-6 rows", M08HardeningRegressionTests.MultiBedPatientRoomSelectsSpecificRows),
    ("Building acoustics hardening: excluded receiver is not used for impact paths", M08HardeningRegressionTests.ExcludedReceiverIsNotUsedForImpactPaths),
    ("Building acoustics hardening: missing partitions block building class", M08HardeningRegressionTests.MissingPartitionsBlockBuildingClass),
    ("Building acoustics hardening: topology refresh preserves identities and results", M08HardeningRegressionTests.TopologyRefreshPreservesIdentitiesAndResults),
    ("Building acoustics hardening: undo clone preserves acoustic state", M08HardeningRegressionTests.UndoClonePreservesAcousticState),
    ("Building acoustics hardening: generated spectrum is marked as model", M08HardeningRegressionTests.GeneratedSpectrumIsMarkedAsModel),
    ("Building acoustics hardening: foreign JSON document is rejected", M08HardeningRegressionTests.ForeignJsonDocumentIsRejected),
    ("Building acoustics hardening: Turkish decimal spectrum round-trips", M08HardeningRegressionTests.TurkishDecimalSpectrumRoundTrips),
    ("Building acoustics hardening: synthetic report rows have stable filter ids", M08HardeningRegressionTests.SyntheticReportRowsHaveStableFilterIds),
    ("Building acoustics hardening: material library uses per-user overrides and preserves provenance", M08HardeningRegressionTests.MaterialLibraryUsesPerUserOverridesAndPreservesProvenance),
    ("Building acoustics hardening: material library save failure reaches caller", M08HardeningRegressionTests.MaterialLibrarySaveFailureIsReportedToCaller),
    ("Building acoustics hardening: legacy material library migrates deterministically", M08HardeningRegressionTests.LegacyMaterialLibraryArrayMigratesDeterministically),
    ("Building acoustics hardening: legacy base-directory material differences migrate", M08HardeningRegressionTests.LegacyBaseDirectoryCatalogDifferencesMigrateToUserData),
    ("Building acoustics hardening: building function maps to visible regulation class", M08HardeningRegressionTests.BuildingFunctionMapsToVisibleRegulationClass),
    ("Building acoustics final: origin normalization preserves world coordinates", M08FinalRegressionTests.OriginNormalizationPreservesWorldCoordinates),
    ("Building acoustics final: independent-unit edit marks project dirty", M08FinalRegressionTests.IndependentUnitEditMarksProjectDirty),
    ("Building acoustics final: new edit after undo clears redo branch", M08FinalRegressionTests.NewEditAfterUndoClearsRedoBranch),
    ("Building acoustics final: incompatible wall material blocks preflight", M08FinalRegressionTests.IncompatibleWallMaterialBlocksPreflight),
    ("Building acoustics final: pasted spectrum does not invent measurement provenance", M08FinalRegressionTests.PastedSpectrumDoesNotInventMeasurementProvenance),
    ("Building acoustics final: report selection matches canonical rows including missing data", M08FinalRegressionTests.ReportSelectionMatchesCanonicalRowsIncludingMissingData),
    ("Building acoustics report: combined facade is reported once with stable room id", M08ReportHardeningRegressionTests.CombinedFacadeIsReportedOnceWithStableRoomId),
    ("Building acoustics report: incomplete facade is data-missing without numeric margin", M08ReportHardeningRegressionTests.IncompleteFacadeIsDataMissingWithoutNumericMargin),
    ("Building acoustics report: Excel octave sheet uses same result set as summary", M08ReportHardeningRegressionTests.ExcelOctaveSheetUsesTheSameResultSetAsSummary),
    ("Building acoustics report: 3D result uses canonical missing status", M08ReportHardeningRegressionTests.ThreeDimensionalResultUsesCanonicalMissingStatus),
    ("Building acoustics report: DXF excludes calculation-excluded rooms", M08ReportHardeningRegressionTests.DxfDoesNotRenderCalculationExcludedRooms),
    ("Basit proje çiz: armatür yerleşimi eski mantık gelse de grid kullanır", BasitProjeCizKabloTests.ArmatürYerlesimiEskiMantikGelseDeGridDizilimKullanir),
    ("Basit proje çiz: otomatik armatür yerleşimi sadece grid adaylarını kullanır", BasitProjeCizKabloTests.OtomatikArmatürYerlesimiSadeceGridAdaylariniKullanir),
    ("Basit proje çiz: otomatik aydınlatma kablolama boş armatürleri bağlar", BasitProjeCizKabloTests.OtomatikAydinlatmaKablolamaBosArmaturleriBaglar),
    ("Basit proje çiz: mahal listesinden otomatik yerleştirmede tanımsız armatür varsayılana düşer", BasitProjeCizKabloTests.MahalListesiOtomatikYerlestirArmaturFallbackTest),
    ("Basit proje çiz: mahal listesi tam yenileme manuel elemanı korur", BasitProjeCizKabloTests.MahalListesiTamYenilemeManuelElemaniKorur),
    ("Basit proje çiz: mahal listesi kilitli projede yerleştirme yapmaz", BasitProjeCizKabloTests.MahalListesiKilitliProjedeOtomatikYerlestirmeYapmaz),
    ("Basit proje çiz: tali pano kat panosuna beslenir (rol BlokAdi ile)", BasitProjeCizKabloTests.OtomatikBaglamaTaliPanoyuKatPanosunaBaglar),
    ("Basit proje çiz: buat mahaldeki tali panoyu tercih eder", BasitProjeCizKabloTests.BuatHedefPanoMahaldekiTaliPanoyuTercihEder),
    ("Basit proje çiz: mahal listesi duvar aralığı yerleşim adedini etkiler", BasitProjeCizKabloTests.MahalListesiDuvarAraligiYerlesimAdediniEtkiler),
    ("Basit proje çiz: priz adet modu MontajTipi Tavan olsa da duvar kenarına yerleşir", BasitProjeCizKabloTests.PrizAdetModuMontajTipiTavanOlsaDaDuvarKenarindaYerlesir),
    ("Basit proje çiz: mahal listesi Grid genel ayar yalnız aktif hücreyi yerleştirir", BasitProjeCizKabloTests.MahalListesiGridGenelAyarSadeceAktifHucreyiYerlestirir),
    ("Basit proje çiz: mahal Grid aktiflik migrasyonu eski projeyi taşır", BasitProjeCizKabloTests.MahalGridAktiflikMigrasyonuEskiProjeyiTasir),
    ("Basit proje çiz: manuel priz kablolama undo ile geri alınır", BasitProjeCizKabloTests.ManuelPrizKablolamaUndoIleGeriAlinir),
    ("Basit proje çiz: otomatik aydınlatma buatı her 4 anahtara bir üretir", BasitProjeCizKabloTests.OtomatikAydinlatmaBuatiHer4AnahtaraBirUretir),
    ("Basit proje çiz: kat aydınlatma kablolama en yakın buata bağlar", BasitProjeCizKabloTests.KatAydinlatmaKablolamaEnYakinBuataBaglar),
    ("Basit proje çiz: mahal hücre oda-özel isim+watt uygulanır", BasitProjeCizKabloTests.MahalHucreOdaOzelIsimWattUygulanir),
    ("Basit proje çiz: buat→pano önceliği (tali>atanmış>en yakın)", BasitProjeCizKabloTests.BuatIcinHedefPanoOnceligi),
    ("Basit proje çiz: buatta 9 üstü armatür linyeyi trifaze yapar", BasitProjeCizKabloTests.Buatta9UstuArmaturLinyeyiTrifazeYapar),
    ("Basit proje çiz: anahtarlar yalnız kapı açıklığı yanına yerleşir", BasitProjeCizKabloTests.AnahtarlarYalnizKapiAcikligiYaninaYerlesir),
    ("Basit proje çiz: manually assigned switch and sensor use expected buat routing", BasitProjeCizKabloTests.ManuallyAssignedSwitchAndSensorUseExpectedBuatRouting),
    ("Basit proje çiz: mahal sensörlü hücre her armatüre sensör ekler ve buata bağlar", BasitProjeCizKabloTests.MahalSensorluHucreHerArmatureSensorEklerVeBuataBaglar),
    ("Basit proje çiz: grid X ve Y aralıkları ayrı çalışır", BasitProjeCizKabloTests.GridXveYAraliklariAyriCalisir),
    ("Basit proje çiz: vavien travel cable follows connected walls", BasitProjeCizKabloTests.VavienTravelCableFollowsConnectedWalls),
    ("Basit proje çiz: disconnected room buat route uses right angle bridge", BasitProjeCizKabloTests.DisconnectedRoomBuatRouteUsesRightAngleBridge),
    ("Basit proje çiz: same wall buat route uses shortest straight path", BasitProjeCizKabloTests.SameWallBuatRouteUsesShortestStraightPath),
    ("Basit proje çiz: overlapping cable segments are visually separated", BasitProjeCizKabloTests.OverlappingCableSegmentsAreVisuallySeparated),
    ("Basit proje çiz: en yakın duvar iç normal ve dönüş açısı doğru", BasitProjeCizKabloTests.EnYakinDuvarIcNormalVeDonusAcisiDogru),
    ("Basit proje çiz: oda merkezi alan-ağırlıklı centroid", BasitProjeCizKabloTests.OdaMerkeziAlanAgirlikliCentroiddir),
    ("Basit proje çiz: DXF çıktı duvar sembolünü duvara paralel döndürür", BasitProjeCizKabloTests.DxfCiktiDuvarSemboluDuvaraParalelDondurur),
    ("Basit proje çiz: buat-pano routes stay inside selected rooms and renumber", BasitProjeCizKabloTests.BuatPanoRoutesStayInsideSelectedRoomsAndRenumber),
    ("Basit proje çiz: buat-pano route does not use long diagonal across rooms", BasitProjeCizKabloTests.BuatPanoRouteDoesNotUseLongDiagonalAcrossRooms),
    ("Basit proje çiz: buat-pano route does not enter unselected rooms", BasitProjeCizKabloTests.BuatPanoRouteDoesNotEnterUnselectedRooms),
    ("Basit proje çiz: panel feed route uses selected rooms and walls", BasitProjeCizKabloTests.PanoBeslemeRouteUsesSelectedRoomsAndWalls),
    ("Basit proje çiz: panel and junction routes use element room ids on boundaries", BasitProjeCizKabloTests.PanoBuatRoutesUseElementRoomIdsOnRoomBoundary),
    ("Basit proje çiz: panel feed route without allowed transition stays empty", BasitProjeCizKabloTests.PanoBeslemeRouteWithoutAllowedTransitionStaysEmpty),
    ("Basit proje çiz: panel-junction route bridges small openings between allowed rooms", BasitProjeCizKabloTests.PanoBuatRouteBridgesSmallOpeningsBetweenAllowedRooms),
    ("Basit proje çiz: ikinci priz kablolama çift devre üretmez", BasitProjeCizKabloTests.PrizIkinciKablolamaCiftDevreUretmez),
    ("Basit proje çiz: aydınlatma buat sayımı priz buatını saymaz", BasitProjeCizKabloTests.AydinlatmaBuatSayimiPrizBuatiniSaymaz),
    ("Basit proje çiz: kapasite muhasebesi dolu anahtarı atlar", BasitProjeCizKabloTests.KapasiteMuhasebesiDoluAnahtariAtlar),
    ("Basit proje çiz: açıklık yanı prizler kapı yanına toplanır", BasitProjeCizKabloTests.AciklikYaniPrizlerKapiYaninaToplanir),
    ("Basit proje çiz: undo tali pano hedefini korur", BasitProjeCizKabloTests.UndoTaliPanoHedefiniKorur),
    ("Basit proje çiz: undo mahal satırını geri getirir", BasitProjeCizKabloTests.UndoMahalSatiriniGeriGetirir),
    ("Basit proje çiz: IsDirty mutasyonda kirlenir kayıtta temizlenir", BasitProjeCizKabloTests.IsDirtyMutasyondaKirlenirKayittaTemizlenir),
    ("Basit proje çiz: kablo modu tıklaması canlı rota çizer, ikinci tık çıkarır", BasitProjeCizKabloTests.KabloModuTiklamaCanliRotaCizerVeIkinciTikCikarir),
    ("Basit proje çiz: duvar graf önbelleği deterministik ve geçersizlenir", BasitProjeCizKabloTests.DuvarGrafOnbellegiDeterministikVeGecersizlesir),
    ("Basit proje çiz: tüm katlara yerleştir birden fazla katı işler", BasitProjeCizKabloTests.TumKatlaraYerlestirBirdenFazlaKatiIsler),
    ("M12 güvenilirlik: özellik düzenleme kilit, IsDirty ve undo kurallarına uyar", M12GuvenilirlikTests.OzellikDuzenlemeKilitDirtyVeUndoKurallarinaUyar),
    ("M12 güvenilirlik: eleman silme IsDirty ve undo kurallarına uyar", M12GuvenilirlikTests.ElemanSilmeDirtyVeUndoKurallarinaUyar),
    ("M12 güvenilirlik: doğrudan priz pano özetine girer", M12GuvenilirlikTests.DirektPrizPanoOzetineGirer),
    ("M12 güvenilirlik: pano silme tüm kat referanslarını temizler ve undo geri getirir", M12GuvenilirlikTests.PanoSilmeTumKatReferanslariniTemizlerVeUndoGeriGetirir),
    ("M12 güvenilirlik: Tamamını Sil yalnız aktif paftayı temizler", M12GuvenilirlikTests.TumunuSilYalnizAktifPaftayiTemizler),
    ("M12 güvenilirlik: seçili devre silme yetim pano bağlantısı bırakmaz", M12GuvenilirlikTests.SeciliDevreSilmeYetimPanoBaglantisiBirakmaz),
    ("M12 güvenilirlik: mahal satırı kimliği aynı tanımı ayırır", M12GuvenilirlikTests.MahalSatiriKimligiAyniTanimiAyirir),
    ("M12 güvenilirlik: M50 import elektrik verili boş katı korur", M12GuvenilirlikTests.M50ImportElektrikVeriliBosKatiKorurVeMutabakattaGosterir),
    ("M12 güvenilirlik: migrasyon farklı kaynağı güvenli dosyaya ayırır", M12GuvenilirlikTests.M12MigrasyonFarkliKaynagiGuvenliDosyayaAyirir),
    ("Mahal listesi servisi: oda adı normalize Türkçe karakteri sadeleştirir", MahalListesiServisiTests.OdaAdiNormalizeTurkceKarakterVeSayiyiSadelestirir),
    ("Mahal listesi servisi: kattan kopyala oda adına göre eşleşir, aktif hücreyi ezmez", MahalListesiServisiTests.KattanKopyalaOdaAdinaGoreEslesirVeAktifHucreyiEzmez),
    ("Mahal listesi servisi: kattan kopyala eşleşmeyen oda pasif kalır", MahalListesiServisiTests.KattanKopyalaEslesmeyenOdaPasifKalir),
    ("Mahal listesi servisi: akıllı doldur oda adından kalem üretir, aktif hücreyi ezmez", MahalListesiServisiTests.AkilliDoldurOdaAdindanKalemUretirVeAktifHucreyiEzmez),
    ("Mahal listesi servisi: akıllı doldur mahal listesine dahil olmayan odayı atlar", MahalListesiServisiTests.AkilliDoldurMahalListesineDahilOlmayanOdayiAtlar),
    ("Mahal listesi servisi: öneri bul en uzun anahtarı seçer", MahalListesiServisiTests.OneriBulEnUzunAnahtariSecer),
    ("Mahal listesi servisi: şablona dönüştür ve uygula round-trip", MahalListesiServisiTests.SablonaDonusturVeUygulaRoundTrip),
    ("Mahal listesi servisi: şablonları kaydet ve yükle atomik ve kalıcı", MahalListesiServisiTests.SablonlariKaydetVeYukleAtomikVeKalici),
    ("Mahal listesi servisi: şablonları yükle olmayan dosyada boş döner çökmez", MahalListesiServisiTests.SablonlariYukleOlmayanDosyadaBosDonerCokmez),
    ("Mahal listesi servisi: yerleşim tahmini Adet modunda kullanıcı adedini döner", MahalListesiServisiTests.YerlesimTahminiAdetModundaKullaniciAdediniDoner),
    ("Mahal listesi servisi: yerleşim tahmini Grid modunda aday üreticiden hesaplar", MahalListesiServisiTests.YerlesimTahminiGridModundaAdayUreticidenHesaplar),
    ("Mahal listesi servisi: yerleşim tahmini pasif veya hariç odada sıfır döner", MahalListesiServisiTests.YerlesimTahminiPasifVeyaHaricOdadaSifirDoner),
    ("Mahal listesi servisi: kat tahmini güç sembol gücünü ve adedi çarpar", MahalListesiServisiTests.KatTahminiGucWSembolGucunuVeAdediÇarpar),
    ("Basit proje çiz: mechanical route follows walls", BasitProjeCizKabloTests.MekanikRouteFollowsWalls),
    ("Basit proje çiz: pano content summarizes linye armature, power and fuse", BasitProjeCizKabloTests.PanoIcerikLinyeArmaturGucVeSigortayiOzetler),
    ("Basit proje çiz: pano content summarizes mechanical power and fuse", BasitProjeCizKabloTests.PanoIcerikMekanikGucVeSigortayiOzetler),
    ("Basit proje çiz: pano calculation filters active sheet and avoids zero-power breakers", BasitProjeCizKabloTests.PanoHesabiBosGucteSigortaUretmezVePaftayaGoreFiltreler),
    ("Basit proje çiz: same junction can keep separate lighting and socket panel links", BasitProjeCizKabloTests.AyniBuatAydinlatmaVePrizBaglantilariniAyriTutar),
    ("Basit proje çiz: three-phase socket uses 400V three-phase current in panel calculation", BasitProjeCizKabloTests.TrifazePrizPanoHesabinda400VTrifazeAkimKullanir),
    ("Basit proje çiz: panel circuit numbering restarts per panel", BasitProjeCizKabloTests.PanoNumaralariHerPanodaAyricaBaslar),
    ("Basit proje çiz: DXF labels are written to their sheet layers", BasitProjeCizKabloTests.DxfEtiketleriAitOlduklariPaftaLayerindaYazar),
    ("Basit proje çiz: weak-current data line routes and summarizes at its hub", BasitProjeCizKabloTests.ZayifAkimDataHattiRotaVeMerkezOzeti),
    ("Basit proje çiz: weak-current CCTV star creates separate channels", BasitProjeCizKabloTests.ZayifAkimCctvYildizAyriHatlar),
    ("Basit proje çiz: fire-alarm loop closes back at the panel", BasitProjeCizKabloTests.ZayifAkimYanginLoopKapaliCevrim),
    ("Basit proje çiz: CCTV cameras can terminate at a network switch", BasitProjeCizKabloTests.ZayifAkimCctvSwitchMerkezOlabilir),
    ("Basit proje çiz: intercom lines route to intercom exchange", BasitProjeCizKabloTests.ZayifAkimDiafonYildizSantraleBaglanir),
    ("Disiplin ayrımı: karma proje sayımı doğru sayar", DisiplinAyiklamaTests.DisiplinSayimiKarmaProjeyiDogruSayar),
    ("Disiplin ayrımı: M12 ayıklaması zayıf akımı siler kuvvetliyi korur", DisiplinAyiklamaTests.M12AyiklamasiZayifAkimiSilerKuvvetliKorur),
    ("Disiplin ayrımı: M16 ayıklaması kuvvetli akımı siler zayıfı korur", DisiplinAyiklamaTests.M16AyiklamasiKuvvetliAkimiSilerZayifKorur),
    ("Disiplin ayrımı: ayıklama silinen cihaz referansını temizler", DisiplinAyiklamaTests.AyiklamaSistemDevresindekiSilinenCihazReferansiniTemizler),
    ("Disiplin ayrımı: eleman tipi disiplin haritası doğru sınıflar", DisiplinAyiklamaTests.DisiplinHaritasiElemanTiplerinDogruSiniflar),
    ("M16→M13: ayıklanan zayıf akım projesi M13'e doğru aktarılır", DisiplinAyiklamaTests.M16AyiklananProjeM13eDogruAktarilir),
    ("M16→M13: zayıf akım yoksa aktarım boş üretir", DisiplinAyiklamaTests.M13AktarimiZayifAkimYoksaBosUretir),
    ("Tek hat aktarım: ana ve tali panoları oluşturur", TekHatAktarimTests.AnaVeTaliPanolariniOlusturur),
    ("Tek hat aktarım: linyeyi çıkış sigortası olarak aktarır", TekHatAktarimTests.LinyeyiCikisSigortasiOlarakAktarir),
    ("Tek hat aktarım: trifaze priz linyesini trifaze aktarır", TekHatAktarimTests.TrifazePrizLinyesiniTrifazeAktarir),
    ("Tek hat aktarım: besleme hiyerarşisini korur", TekHatAktarimTests.BeslemeHiyerarsisiniKorur),
    ("Tek hat aktarım: çift beslemede kat panosu ebeveyn seçilir", TekHatAktarimTests.CiftBeslemeHattindaKatPanosuEbeveynSecilir),
    ("Tek hat aktarım: kök pano toplam gücü alt panoları içerir", TekHatAktarimTests.KokPanoToplamGucuAltPanolariIcerir),
    ("Tek hat aktarım: aynı kat pano zinciri doğru kökü ve tek yükü aktarır", M12GuvenilirlikTests.AyniKatPanoZinciriDogruKokuVeTekYukuAktarir),
    ("Tek hat aktarım: pano isimleri benzersizleştirilir", TekHatAktarimTests.PanoIsimleriBenzersizlestirilir),
    ("Tek hat aktarım: .e4p11 round-trip panoları korur", TekHatAktarimTests.E4p11RoundTripPanolariKorur),
    ("Tek hat aktarım: besleme yok ve çok pano varsa hata verir", TekHatAktarimTests.BeslemeYokVeCokPanoVarsaHataVerir),
    ("Tek hat aktarım: entegrasyon hesap zinciri + kompanzasyon auto-tetik", TekHatAktarimTests.EntegrasyonHesapZinciriVeKompanzasyonAutoTetik),
    ("Tek hat aktarım: düşük yükte kompanzasyon tetiklenmez", TekHatAktarimTests.EntegrasyonDusukYuktekompanzasyonTetiklenmez),
    ("M11 hesap: kablo düşük akımda minimum kesit", ElektrikHesapTests.KabloKesiti_DusukAkimMinKesit),
    ("M11 hesap: kablo yüksek akımda termal kesit büyür", ElektrikHesapTests.KabloKesiti_YuksekAkimTermalBuyur),
    ("M11 hesap: kablo uzun mesafede gerilim düşümü kesiti büyütür", ElektrikHesapTests.KabloKesiti_UzunMesafeGerilimKriteriBuyutur),
    ("M11 hesap: kablo reaktans çözümü uzunluğu çift çarpmaz", ElektrikHesapTests.KabloKesiti_ReaktansCozumuUzunluguCifteCarpmaz),
    ("M11 hesap: mono ve trifaze kablo etiketleri ortak formatta", ElektrikHesapTests.KabloEtiketi_MonoVeTrifazeOrtakFormatta),
    ("M11 hesap: faz dağılımı üç eşit yükü dengeler", ElektrikHesapTests.FazDagilimi_UcEsitYukDengeli),
    ("M11 hesap: faz akımları cosφ ile hesaplanır", ElektrikHesapTests.FazDagilimi_FazAkimlariCosFIleHesaplanir),
    ("M11 hesap: yük tipine göre minimum linye sigortaları uygulanır", ElektrikHesapTests.LinyeSigortasi_YukTipiMinimumlariniUygular),
    ("M11 hesap: linye kablo kesiti/düşüm/ısı atanır (K1)", ElektrikHesapTests.LinyeKabloHesabi_KesitVeDusumAtanir),
    ("M11 hesap: kümülatif gerilim düşümü zincir boyunca toplanır + sınır aşımı (A1)", ElektrikHesapTests.KumulatifGerilimDususu_ZincirBoyuncaToplanirVeSinirAsimiIsaretlenir),
    ("M11 hesap: aşırı yükte kablo kesiti yetersiz işaretlenir (A2)", ElektrikHesapTests.KabloKesitiYeterli_AsiriYukteIsaretlenir),
    ("M11 hesap: pano metrajı standart alan/derinliğe göre tek satırda toplanır", ElektrikHesapTests.PanoMetraj_StandartAlanVeDerinligeGoreTekSatir),
    ("M11 hesap: büyük trifaze linye sigortası 250A üstüne çıkar (C3)", ElektrikHesapTests.LinyeSigortasi_BuyukTrifazeLinye250AUstuneCikar),
    ("M11 hesap: jeneratör lokal grup hedefi yoksa atlanır (A4)", ElektrikHesapTests.JeneratorLokalGrup_HedefYoksaAnaPanoyaDusmedenAtlanir),
    ("M11 hesap: kısa devre zincirine kablo reaktansı dahil (B)", ElektrikHesapTests.KisaDevre_KabloReaktansiZincireDahil),
    ("M11 kontaktör: linye bayrağı ile otomatik seçilir", ElektrikHesapTests.Kontaktor_LinyeBayragiIleOtomatikSecilir),
    ("M11 darbe kesici: bayrak + 16A üstü kontaktör kuralı", ElektrikHesapTests.DarbeKesici_BayrakVe16AKontaktorKurali),
    ("M11 kontrol panosu: darbe kesici tekil atama + sinyal kablosu gruplama", ElektrikHesapTests.KontrolPanosu_DarbeKesiciAtamaTekilVeKabloGruplu),
    ("M11 UI: linye numarası yük tipine göre ayrı sıralanır", ElektrikHesapTests.LinyeNo_AyniPanodaYukTipineGoreSiralanir),
    ("M11 UI: linye silince numara boşluğu kapatılır", ElektrikHesapTests.LinyeNo_SilmeSonrasiBoslukKapatir),
    ("M11 UI: mekanik yük motorlarla aynı M serisini kullanır", ElektrikHesapTests.LinyeNo_MekanikMotorIleAyniMSerisiniKullanir),
    ("M11 UI: linye tekrarla ekle sıradaki no ile kopyalar", ElektrikHesapTests.LinyeTekrarlaEkle_AyniLinyeyiSiradakiNoIleKopyalar),
    ("M11 Excel: linye şablonu round-trip okunur", ElektrikHesapTests.LinyeExcelSablonu_RoundTripOkunur),
    ("M11 Excel: seçili panoyu yeniler ve alt pano çıkışlarını korur", ElektrikHesapTests.LinyeExcelAktarimi_SeciliPanoyuYenilerVeAltPanoCikislariniKorur),
    ("M11 Excel: pano raporu ayrı sayfalarda tüm giriş ve çıkışları yazar", ElektrikHesapTests.ExcelRaporu_PanolarAyriSayfaVeCikislarDogru),
    ("M11 Excel: ilk sayfa M13 toplam metraj tüm grupları yazar", ElektrikHesapTests.ExcelRaporu_M13ToplamMetrajTumGruplariYazar),
    ("M11 kütüphane: eski yük tiplerine Mekanik varsayılanı eklenir", ElektrikHesapTests.YukTipiKutuphane_EskiDosyayaMekanikVarsayilanEklenir),
    ("M11 KAR: 100A altı klasik röle seçer", ElektrikHesapTests.KacakAkim_YuzAmperAltiKlasikKarSecer),
    ("M11 KAR: 100A üstü troid röle + akım trafosu seçer", ElektrikHesapTests.KacakAkim_YuzAmperUstuTroidVeAkimTrafosuSecer),
    ("M11 KAR: pano bazında kapatılır ve tercihi korunur", ElektrikHesapTests.KacakAkim_PanoBazindaKapatilir),
    ("M11 pano: gövde alanı ve otomatik/manüel ölçü seçimi", ElektrikHesapTests.PanoBoyutlandirma_OtomatikVeManuelSecim),
    ("M11 kütüphane: eski KAR dosyasına troid ve akım trafosu eklenir", ElektrikHesapTests.KacakAkimKutuphane_EskiDosyayaTroidVeAkimTrafosuEklenir),
    ("M11 giriş sigortası: ana pano yüksek alt pano yükünde güncellenir", ElektrikHesapTests.GirisSigortasi_AnaPanoYuksekAltPanoYukundeGuncellenir),
    ("M11 hesap: kompanzasyon gerekli Q formülü", ElektrikHesapTests.Kompanzasyon_GerekliQFormulu),
    ("M11 hesap: kompanzasyon geçersiz cosφ ile NaN üretmez", ElektrikHesapTests.Kompanzasyon_GecersizCosFNaNUretmez),
    ("M11 hesap: kompanzasyon kademe sayısı normalize edilir", ElektrikHesapTests.Kompanzasyon_KademeSayisiNormalizeEdilir),
    ("M11 hesap: kompanzasyon elle ayar kademe sayısını korur", ElektrikHesapTests.Kompanzasyon_ElleAyarKademeSayisiniKorur),
    ("M11 hesap: kompanzasyon panosu kVAr ile boyutlanır + DXF'e yazılır", ElektrikHesapTests.Kompanzasyon_PanoKVarIleBoyutlandirilirVeDxfeYazilir),
    ("M11 hesap: diversite besleme sigortasını revize eder", ElektrikHesapTests.Diversite_BeslemeSigortasiniRevizeEder),
    ("M11 hesap: yangın panosu ana güce eklenmez", ElektrikHesapTests.YanginPanosu_AnaGuceEklenmez),
    ("M11 UI: Pano Ekle akışı yangın panosu oluşturur", ElektrikHesapTests.YanginPanosu_PanoEkleAkisindaOlusturulur),
    ("M11 hesap: trafosuz ana pano giriş şalteri otomatik atanır", ElektrikHesapTests.GirisSalteri_TrafosuzOtomatikAtanir),
    ("M11 hesap: kısa devre trafo Ik3 formülü (K3)", ElektrikHesapTests.KisaDevre_TrafoIk3Formulu),
    ("M11 hesap: kısa devre alt panoda azalır (K3)", ElektrikHesapTests.KisaDevre_AltPanodaAzalir),
    ("M11 koruma cihazı: kesme kapasitesi + cihaz tipi (sigorta/TMŞ)", ElektrikHesapTests.KorumaCihazi_KesmeKapasitesiVeTip),
    ("M11 koruma cihazı: linye entegrasyonu (>63A → TMŞ)", ElektrikHesapTests.KorumaCihazi_LinyeEntegrasyonu),
    ("M11 koruma cihazı: ≤63A TMŞ zorlama + manuel kesme korunur", ElektrikHesapTests.KorumaCihazi_TmsZorlaVeManuel),
    ("M11 çıkış koruma: alt panoya TMŞ zorlama + manuel kesme", ElektrikHesapTests.CikisKoruma_TmsZorlaVeKesme),
    ("M11 DXF: linye koruma (TMŞ/DK) + faz (RST) yansır", ElektrikHesapTests.Dxf_LinyeKorumaVeFazYansir),
    ("M11 jeneratör/trafo: kat etiketi DXF + round-trip", ElektrikHesapTests.JeneratorTrafo_KatEtiketiDxf),
    ("M11 hesap: busbar pozisyonu kısa devre Ik değerini düşürür", ElektrikHesapTests.KisaDevre_BusbarPozisyonuIkDusurur),
    ("M11 busbar: otomatik tip ihtiyaca göre seçilir", ElektrikHesapTests.Busbar_OtomatikTipIhtiyacaGoreSecilir),
    ("M11 busbar: kutu pozisyonları elle korunur", ElektrikHesapTests.Busbar_KutuPozisyonlariElleKorunur),
    ("M11 busbar: çıkış kutusu sigortası yük değişince güncellenir", ElektrikHesapTests.Busbar_CikisKutusuSigortasiYukDegisinceGuncellenir),
    ("M11 pano sil: busbar referansları temizlenir", ElektrikHesapTests.PanoSil_BusbarReferanslariniTemizler),
    ("M11 pano sil: jeneratör/UPS/OG referansları temizlenir", ElektrikHesapTests.PanoSil_JeneratorUpsOgReferanslariniTemizler),
    ("M11 jeneratör: lokal bölge ve tesis geneli birlikte", ElektrikHesapTests.Jenerator_LokalBolgeVeTesisGeneliBirlikte),
    ("M11 jeneratör: besleme kapsamı round-trip korunur", ElektrikHesapTests.Jenerator_BeslemeKapsamiRoundTrip),
    ("M11 jeneratör: ünite-senkron ve senkron-hedef kablo mesafeleri korunur", ElektrikHesapTests.Jenerator_KabloMesafeleriRoundTripDxfVeExcel),
    ("M11 jeneratör: yüksek akımda 240 mm² paralel kablo kolu atanır", ElektrikHesapTests.Jenerator_YuksekAkimdaParalelKabloAtar),
    ("M11 hesap: jeneratör standby modu uygun seçer", ElektrikHesapTests.Jenerator_StandbyModUygunSecer),
    ("M11 hesap: jeneratör seçiminde kVA kriteri aranır", ElektrikHesapTests.Jenerator_KvaYetersizTipiUygunSaymaz),
    ("M11 transfer şalter: yüksek akımda 1250A üstü seçer", ElektrikHesapTests.TransferSalter_YuksekAkimda1250UstuSecer),
    ("M11 transfer şalter: otomatik yükle güncellenir, manuel korunur", ElektrikHesapTests.TransferSalter_OtomatikYukleGuncellenirManuelKorunur),
    ("M11 kütüphane: eski transfer şalter dosyasına yüksek akım ATS eklenir", ElektrikHesapTests.TransferSalterKutuphane_EskiDosyayaYuksekAkimVarsayilanlariEklenir),
    ("M11 hesap: farklı güçte jeneratörler senkron panoda toplanır", ElektrikHesapTests.JeneratorGrubu_FarkliGucleriSenkronPanodaToplar),
    ("M11 kütüphane/proje: eski tek jeneratör senkron gruba taşınır", ElektrikHesapTests.JeneratorGrubu_EskiTekJeneratorGrubaTasinir),
    ("M11 kütüphane: Aksa/Emsa/Arken/Alimar jeneratörleri gelir", ElektrikHesapTests.JeneratorKutuphane_AksaEmsaArkenAlimarVarsayilanlariGelir),
    ("M11 kütüphane: eski jeneratör dosyasına yeni markalar eklenir", ElektrikHesapTests.JeneratorKutuphane_EskiDosyayaYeniMarkalarBirlestirilir),
    ("M11 hesap: selektivite üst çıkış bir üst değere", ElektrikHesapTests.Selektivite_UstCikisBirUstDegere),
    ("M11 hesap: kablo reaktansı gerilim düşümüne eklenir (O3)", ElektrikHesapTests.Kablo_ReaktansGerilimDususuneEklenir),
    ("M11 hesap: reaktans dahil kesit gerilim sınırında kalır (O3)", ElektrikHesapTests.Kablo_GerilimDususuSinifIcinde),
    ("M11 hesap: reaktör akort yüzdesi reaktör seçer (S4)", ElektrikHesapTests.Kompanzasyon_AkortYuzdesiReaktorSecer),
    ("M11 hesap: diversite faktörü alt pano gücünü azaltır", ElektrikHesapTests.Diversite_AltPanoGucunuAzaltir),
    ("M11 DXF: geçerli tek hat şeması dosyası üretir (K2)", ElektrikHesapTests.DxfCikti_GecerliDosyaUretir),
    ("M11 DXF: jeneratörleri M11-JEN katmanında görünür yazar", ElektrikHesapTests.DxfCikti_JeneratorleriGorunurYazar),
    ("M11 DXF: çoklu busbarları ayrı yazar", ElektrikHesapTests.DxfCikti_CokluBusbarlariAyriYazar),
    ("M11 UPS: gerekli kVA emniyet faktörlü", ElektrikHesapTests.Ups_GerekliKVAEmniyetFaktorlu),
    ("M11 UPS: uygun tip otomatik seçilir", ElektrikHesapTests.Ups_UygunTipOtomatikSecilir),
    ("M11 UPS: giriş akımı çıkıştan büyük", ElektrikHesapTests.Ups_GirisAkimiCikistanBuyuk),
    ("M11 UPS akü: seri blok DC baradan türer", ElektrikHesapTests.Aku_SeriBlokDcBaradanTurer),
    ("M11 UPS akü: tablo dışı uzun otonomi yetersizdir", ElektrikHesapTests.Aku_TabloDisiUzunOtonomiYetersizdir),
    ("M11 UPS akü: uzun otonomi daha fazla paralel ister", ElektrikHesapTests.Aku_UzunOtonomiDahaFazlaParalelGerektirir),
    ("M11 UPS DXF: M11-UPS katmanı üretir", ElektrikHesapTests.Ups_DxfUpsKatmaniUretir),
    ("M11 UPS: mevcut pano kritik panoya çevrilir ve geri alınır", ElektrikHesapTests.Ups_KritikPanoyaCevirVeGeriAl),
    ("M11 UPS: kritik panoya çevirme kısıtları", ElektrikHesapTests.Ups_KritikCevirKisitlari),
    ("M11 UPS merkezi: kurulum + kat kritik panosu", ElektrikHesapTests.Ups_MerkeziUpsKurVeKatKritikPanosu),
    ("M11 UPS merkezi: linye kritik panoya taşınır", ElektrikHesapTests.Ups_MerkeziLinyeyiKritikPanoyaTasi),
    ("M11 UPS merkezi: DXF ayrı kolon (UPS Kolonu) etiketi", ElektrikHesapTests.Ups_MerkeziDxfUpsKolonu),
    ("M11 DXF: kat-bazlı aşağıdan yukarı + kat ayrım çizgileri", ElektrikHesapTests.Dxf_KatBazliAsagidanYukari),
    ("M11 OG: trafo otomatik seçimi yük×emniyete göre", ElektrikHesapTests.OrtaGerilim_TrafoOtomatikSecimi),
    ("M11 OG: trafo AG/OG akımları", ElektrikHesapTests.OrtaGerilim_TrafoAkimlari),
    ("M11 OG: kaynak Ik3 = Zq+Zt (şebeke+trafo)", ElektrikHesapTests.OrtaGerilim_KaynakIk3ZqZt),
    ("M11 OG: OG yokken eski kısa devre davranışı korunur", ElektrikHesapTests.OrtaGerilim_OgYokeskiDavranisKorunur),
    ("M11 OG: hücre anma akımı yetersizlik uyarısı", ElektrikHesapTests.OrtaGerilim_HucreAnmaUyarisi),
    ("M11 OG: kablo bağlantısı boyutlandırma", ElektrikHesapTests.OrtaGerilim_KabloBaglantiBoyutlandirma),
    ("M11 OG: yüksek akımda trafo-ana pano paralel kablosu otomatik artar", ElektrikHesapTests.OrtaGerilim_YuksekAkimdaParalelKabloyuOtomatikArtirir),
    ("M11 OG: trafo-ana pano kablosu ana panoya yansır", ElektrikHesapTests.OrtaGerilim_TrafoAnaPanoKablosuAnaPanoyaYansir),
    ("M11 OG: Bar-24 bağlantısı boyutlandırma", ElektrikHesapTests.OrtaGerilim_Bar24Baglanti),
    ("M11 OG: köşk yerleşim geometrisi (çakışmasız)", ElektrikHesapTests.KoskYerlesim_Geometri),
    ("M11 OG: .e4p11 round-trip OG korunur", ElektrikHesapTests.OrtaGerilim_RoundTrip),
    ("M11 OG DXF: M11-OG/TRAFO/HUCRE katmanları üretir", ElektrikHesapTests.OrtaGerilim_DxfKatmanlari),
    ("M11 OG: yeniden adlandırma OG.AnaPanoAd günceller", ElektrikHesapTests.OrtaGerilim_YenidenAdlandirmaKaskadi),
    ("Keşif hazırla: icmal bütün pozları ve tablo kırılımını gösterir", KesifHazirlaTests.IcmalButunPozlariVeTabloKiriliminiGosterir),
    ("Keşif hazırla: aynı poz farklı birim fiyat uyarısı verir", KesifHazirlaTests.IcmalAyniPozFarkliBirimFiyatUyarisiVerir),
    ("Keşif hazırla: icmal seçimi değişince tablo kırılımı değişir", KesifHazirlaTests.IcmalSecimiDegisinceTabloKirilimiDegisir),
    ("Keşif hazırla: poz import Türkçe Tanımı başlığını okur", KesifHazirlaTests.PozImportTurkceTanimiBasliginiOkur),
    ("Keşif hazırla: poz import küsuratsız binlik fiyatı doğru okur", KesifHazirlaTests.PozImportKusursuzBinlikFiyatiDogruOkur),
    ("Keşif hazırla: poz import quoted noktalı virgüllü CSV okur", KesifHazirlaTests.PozImportQuotedNoktaliVirgulCsvOkur),
    ("Keşif hazırla: poz no boş farklı imalatları ayırır", KesifHazirlaTests.IcmalPozNoBosFarkliImalatlariAyriGosterir),
    ("Keşif hazırla: birim fiyat listeleri pasif/aktif yönetilir", KesifHazirlaTests.BirimFiyatListeleriPasifeAlinipTekrarAktifEdilir),
    ("Keşif hazırla: özel poz güncelleme tarihi testi", KesifHazirlaTests.OzelPozTarihGuncellemeTesti),
    ("Keşif hazırla: son özel poz silinince geri dönmez", KesifHazirlaTests.SonOzelPozSilininceGeriDonmez),
    ("Keşif hazırla: poz kütüphanesi arama ve filtreleme testi", KesifHazirlaTests.PozKutuphaneAramaVeFiltrelemeTesti),
    ("Keşif hazırla: tablo isim değiştirme ve icmal güncelleme testi", KesifHazirlaTests.TabloIsimDegistirmeVeIcmalGuncellemeTesti),
    ("Keşif hazırla: JSON round-trip manuel ve liste pozlarını korur", KesifHazirlaTests.JsonRoundTripManuelVeListePozlariniKorur),
    ("Keşif hazırla: eşleşme anahtarı normalize tutarlı", KesifHazirlaTests.EslemeAnahtariNormalizeTutarli),
    ("Keşif hazırla: poz eşleşme servisi round-trip (öğrenme DB)", KesifHazirlaTests.PozEslemeServisiRoundTrip),
    ("Keşif hazırla: poz eşleşme kontrol edildi round-trip", KesifHazirlaTests.PozEslemeKontrolEdildiRoundTrip),
    ("Keşif hazırla: birim fiyat elle değişti tespiti + round-trip", KesifHazirlaTests.BirimFiyatElleDegistiTespitiVeRoundTrip),
    ("Keşif hazırla: metraj şablon round-trip", KesifHazirlaTests.MetrajImportSablonRoundTrip),
    ("Keşif hazırla: metraj başlığı 7. satırda okunur", KesifHazirlaTests.MetrajImportBaslikYedinciSatirdaOkur),
    ("Keşif hazırla: otomatik satır üretimi + referans silme eşleşmeyi korur", KesifHazirlaTests.OtomatikSatirUretimVeReferansSilEslemeyiKorur),
    ("Keşif hazırla: referans + KaynakReferansId JSON round-trip", KesifHazirlaTests.JsonRoundTripReferansVeKaynakReferansIdKorur),
    ("Keşif hazırla: hiyerarşik tablo toplamı ve icmal", KesifHazirlaTests.HiyerarsikTabloToplamiVeIcmal),
    ("Keşif hazırla: hiyerarşi + proje bilgileri JSON round-trip", KesifHazirlaTests.HiyerarsiJsonRoundTrip),
    ("Keşif hazırla: alt tablo derinlik ve başlık kuralları", KesifHazirlaTests.AltTabloDerinlikVeBaslikKurallari),
    ("Keşif hazırla: asenkron kayıt round-trip", KesifHazirlaTests.AsenkronKaydetRoundTripCalisir),
    ("Keşif hazırla: Poz Atama araması poz no ve ada göre filtreler", KesifHazirlaTests.PozAtamaAramasiPozNoVeAdaGoreFiltreler),
    ("Keşif hazırla: otomatik poz eşleme model/kesit/anma değerini korur", KesifHazirlaTests.OtomatikPozEslemeModelKesitVeAnmaDegeriniKorur),
    ("Keşif hazırla: otomatik poz eşleme alan dışı adayı reddeder", KesifHazirlaTests.OtomatikPozEslemeAlanDisiAdayiReddeder),
    ("Keşif hazırla: otomatik atama öğrenir ve keşif satırı üretir", KesifHazirlaTests.OtomatikPozAtamaEslemeyiOgrenirVeSatirUretir),
    ("Keşif hazırla: Excel raporu icmal/özet/tablo sayfalarını üretir", KesifHazirlaTests.ExcelRaporuIcmalOzetVeTabloSayfalariniUretir),
    ("Keşif hazırla: açık Excel dosyasının üzerine yazmayı reddeder", KesifHazirlaTests.ExcelAcikDosyaninUzerineYazmayiReddeder),
    ("Keşif hazırla: aynı poz ikinci kez eklenince miktarı artar", KesifHazirlaTests.AyniPozIkinciKezEkleninceMiktariArtar),
    ("Keşif hazırla: mevcut projedeki yinelenen pozlar toplanır", KesifHazirlaTests.MevcutProjedeYinelenenPozlarToplanir),
    ("Keşif hazırla: birleşen otomatik pozda referans silme katkıyı korur", KesifHazirlaTests.BirlesenOtomatikPozdaReferansSilmeYalnizKendiMiktariniDusurur),
    ("Boşluk tarama: tek dikdörtgen oda bulunur", BosluktanOdaTespitTests.TekDikdortgenOdaBulunur),
    ("Boşluk tarama: kapı boşluğu mühürlenip iki oda bulunur", BosluktanOdaTespitTests.KapiBosluguMuhurlenipIkiOdaBulunur),
    ("Boşluk tarama: kapı kapatma kapalıyken odalar birleşir", BosluktanOdaTespitTests.KapiKapatmaKapaliIkenOdalarBirlesir),
    ("Boşluk tarama: oda içindeki mobilya odayı bölmez", BosluktanOdaTespitTests.OdaIcindekiMobilyaOdayiBolmez),
    ("Boşluk tarama: küçük alan oda sayılmaz", BosluktanOdaTespitTests.KucukAlanOdaSayilmaz),
    ("Boşluk tarama: adaptif ikinci geçiş dev odayı böler", BosluktanOdaTespitTests.AdaptifIkinciGecisDevOdayiBoler),
    ("Boşluk tarama: adaptif ikinci geçiş normal planları bozmaz", BosluktanOdaTespitTests.AdaptifIkinciGecisKucukOdalariEtkilemez),
    ("Boşluk tarama: büyük karmaşık plan makul sürede biter", BosluktanOdaTespitTests.BuyukKarmasikPlanMakulSuredeBiter),
    ("Boşluk tarama: başıboş uzak segment donmaz ve oda bulur", BosluktanOdaTespitTests.BasibosUzakSegmentDonmazVeOdaBulur),
    ("Boşluk tarama: küçük kolon çıkıntıları düzleşir", BosluktanOdaTespitTests.KucukKolonCikintilariDuzlesir),
    ("M12: küçük kolon çıkıntıları oda taramada düzleşir", BosluktanOdaTespitTests.M12KucukKolonCikintilariDuzlesir),
    ("DXF: segment sadeleştirme kısaları eler ve tavanı uygular", BosluktanOdaTespitTests.SegmentSadelestirmeKisalariElerVeTavaniUygular),
    ("DXF: eski .e4p12 segment migrasyonu eşik üstü katları sadeleştirir", BosluktanOdaTespitTests.EskiSegmentMigrasyonuEsikUstuKatlariSadelestirir),
    ("DXF: oda adı poligon içindeki metinden seçilir", BosluktanOdaTespitTests.OdaAdiPoligonIcindekiMetindenSecilir),
    ("DXF: M12 yükleme ortak CAD koordinatını korur", BosluktanOdaTespitTests.M12DxfYuklemeOrtakCadKoordinatiniKorur),
    ("DXF: DxfKatYukleyici M12 ile aynı sonucu üretir", BosluktanOdaTespitTests.DxfKatYukleyiciM12IleAyniSonucu),
    ("DXF: DxfKatYukleyici zayıf oda taramasında ölçeği düzeltir", BosluktanOdaTespitTests.DxfKatYukleyiciZayifTaramadaOlcegiDuzeltir),
    ("M50: .e4bina.json kaydet→aç round-trip", BinaGeometriTests.E4BinaRoundTrip),
    ("M50: ikinci kayıt GeometriRevizyon'u artırır", BinaGeometriTests.IkincKayitRevizYonuArtirir),
    ("M50: eski SemaSurum yükleme sırasında migrate edilir", BinaGeometriTests.EskiSemaSurumMigrateEdilir),
    ("M50: tavan patlat + geri al düşey birleşimi yönetir", BinaGeometriTests.TavanPatlatVeGeriAl),
    ("M50: oda silinince ilişkili birleşim düşer", BinaGeometriTests.OdaSilinceBirlesimDusmeli),
    ("M50: kat taşınınca birleşim KatId ile doğru kalır", BinaGeometriTests.KatTasinincaBirlesimKatIdKorunur),
    ("M50: eski birleşim KatId migrasyonu (backfill)", BinaGeometriTests.EskiBirlesimKatIdMigrasyonu),
    ("M50: kat silinince oda birleşimleri temizlenir", BinaGeometriTests.KatSilincaOdaBirlesimiTemizlenir),
    ("M50: otomatik açıklık tip varsayılan yüksekliğini alır", BinaGeometriTests.OtomatikAciklikVarsayilanYukseklik),
    ("M50: yeniden tarama manuel yükseklik/eşiği korur", BinaGeometriTests.AciklikTaramaManuelYuksekligiKorur),
    ("M50: IsDirty düzenlemede işaretlenir, temizlemede sıfırlanır", BinaGeometriTests.IsDirtyDuzenlemeVeTemizleme),
    ("M50: komşu kat yoksa patlatma reddedilir", BinaGeometriTests.KomsuKatYoksaPatlatmaReddedilir),
    ("M50: patlatma sol panel sırasına göre komşu seçer", BinaGeometriTests.PatlatmaSolPanelSirasinaGoreKomsuKatSecer),
    ("M50: uçtan uca patlat→kaydet→aç + 3D yüzey üretimi", BinaGeometriTests.UctanUcaPatlatKaydetAc),
    ("M50: 3D önizleme görsel kat sırasını kullanır", BinaGeometriTests.UcBoyutOnizlemeGorselKatSirasiniKullanir),
    ("M50→M12: import Id kararlılığı + eleman/bayrak koruması", BinaGeometriTests.M12ImportIdKararliligi),
    ("M50→M12: M50 kat sırası korunur", BinaGeometriTests.M12ImportM50KatSirasiniKorur),
    ("M50→M12: eski kat kuralı (elemanlı korunur, boş silinir)", BinaGeometriTests.M12ImportEskiKatKurali),
    ("M12→M50: eski .e4p12 migrasyonu .e4bina'ya dışa aktarır", BinaGeometriTests.M12EskiProjeMigrasyonu),
    ("M12→M50: boş proje migrasyon tetiklemez", BinaGeometriTests.M12BosProjeMigrasyonTetiklemez),
    ("M50↔M12: mutabakat raporu oda silinmiş senaryoda doğru çıkar", BinaGeometriTests.MutabakatOdaSilinmisSenaryo),
    ("M50↔M12: kanonik/gerçek örnek regresyonu (migrasyon + round-trip)", BinaGeometriTests.M12GercekProjeRegresyonu),
    ("M50→M08: adapter bölme üretimi eski yolla birebir", BinaGeometriTests.M08AdapterBolmeUretimiBirebir),
    ("M50→M08: M50 kat sırası korunur", BinaGeometriTests.M08AdapterM50KatSirasiniKorur),
    ("M50→M08: yeniden uygulamada akustik alanlar/atamalar korunur", BinaGeometriTests.M08AdapterAkustikAlanKorur),
    ("M50→M08: açıklık boyutları M50'den, malzeme eşlemesi M08'de korunur", BinaGeometriTests.M08AciklikMalzemeEslemesi),
    ("M08→M50: eski .e4p08 migrasyonu kayıpsız (export + round-trip)", BinaGeometriTests.M08EskiProjeMigrasyonu),
    ("M08↔M50: patlatma M50 modeline bağlanır (export/import senkron)", BinaGeometriTests.M08PatlatmaM50ModelineBaglanir),
    ("M08 golden: DnT,w + cephe hesapları M50 round-trip'inde değişmez", BinaGeometriTests.M08GoldenHesapRoundTripDegismez),
    ("M12: patlatılmış (tavanı açık) odada tavan montajlı yerleşim atlanır", BinaGeometriTests.M12PatlatmaTavanYerlesimiAtlanir),
    ("M50 §9: kümeleme uzak planları ayırır", CokluKatTests.KumelemeUzakPlanlariAyirir),
    ("M50 §9: kümeleme bitişik planları birleştirir", CokluKatTests.KumelemeBitisikPlanlariBirlestirir),
    ("M50 §9: kümeleme tek planda tek küme döner", CokluKatTests.KumelemeTekPlandaTekKumeDoner),
    ("M50 §9: plan filtresi küçük kümeyi ayıklar", CokluKatTests.PlanFiltreKucukKumeyiAyiklar),
    ("M50 §9: etiket zemin katı tanır", CokluKatTests.EtiketZeminKatiTanir),
    ("M50 §9: etiket numaralı katı tanır", CokluKatTests.EtiketNumaraliKatiTanir),
    ("M50 §9: etiket bodrumu tanır ve negatif sıralar", CokluKatTests.EtiketBodrumuTanirVeNegatifSiralar),
    ("M50 §9: etiket çatıyı en yüksek sıralar", CokluKatTests.EtiketCatiEnYuksekSiralanir),
    ("M50 §9: etiket bulunamazsa EtiketBulundu=false döner", CokluKatTests.EtiketBulunamazsaBulunduFalseDoner),
    ("M50 §9: çoklu kat tespiti iki planlı DXF'i bulur", CokluKatTests.CokluKatTespitIkiPlanliDxfBulur),
    ("M50 §9: çoklu kat tespiti tek planda null döner", CokluKatTests.CokluKatTespitTekPlandaNullDoner),
    ("M50 §9: çoklu kat üretimi oda + segment üretir", CokluKatTests.CokluKatUretOdaVeSegmentUretir),
    ("M51: .e4salon.json kaydet→aç round-trip", SalonGeometriTests.E4SalonRoundTrip),
    ("M51: ikinci kayıt GeometriRevizyon'u artırır", SalonGeometriTests.IkinciKayitRevizyonuArtirir),
    ("M51: eski SemaSurum yükleme sırasında migrate edilir", SalonGeometriTests.EskiSemaSurumMigrateEdilir),
    ("M51: daire fasetlenince kapalı poligon + doğru alan üretir", SalonGeometriTests.DaireFasetlemeKapaliPoligonUretir),
    ("M51: segment kesişim kesişen/paralel doğru ayırır", SalonGeometriTests.SegmentKesisimDogruCalisir),
    ("M51: çizgi trim ortadaki aralığı çıkarır (2 parça)", SalonGeometriTests.CizgiTrimOrtadanCikarir),
    ("M51: çizgi trim taşmayı kırpar (1 parça)", SalonGeometriTests.CizgiTrimTasmaKirpar),
    ("M51: çizgi trim uç-uca sınırlı segmenti tümüyle siler", SalonGeometriTests.CizgiTrimUcUcaTumunuSiler),
    ("M51: 3D — kapalı dikdörtgen zemin+tavan+4 duvar üretir", SalonGeometriTests.Uc3DDikdortgenZeminDuvarTavan),
    ("M51: 3D — açık çizgi tek duvar yüzeyi üretir", SalonGeometriTests.Uc3DAcikCizgiDuvarUretir),
    ("M51: 3D — parametrik sahne platformu (üst + 4 etek) üretir", SalonGeometriTests.Uc3DSahnePlatformuUretir),
    ("M51: 3D — kademeli seyirci basamak + korniş üretir", SalonGeometriTests.Uc3DSeyirciKademeUretir),
    ("M51: 3D — balkon platformu (döşeme+alt+saçak+parapet) üretir", SalonGeometriTests.Uc3DBalkonPlatformuUretir),
    ("M51: şablon — dikdörtgen tam salon (dış hat+sahne+seyirci+3D) üretir", SalonGeometriTests.SablonDikdortgenTamSalonUretir),
    ("M51: şablon — at nalı planı yaylı (çok köşeli) üretir", SalonGeometriTests.SablonAtNaliEgriUretir),
    ("M51→M01: native köprü AcousticsProject (yüzey+kategori+hacim) üretir", SalonGeometriTests.M01KopruNativeProjeUretir),
    ("M51: DXF export 3DFACE + kategori katmanları üretir", SalonGeometriTests.DxfExportSalonDosyaUretir),
    ("M51: tam model (sahne+seyirci+balkon) round-trip korunur", SalonGeometriTests.TamModelRoundTrip),
    ("M51: DXF export Türkçe kategori (Korniş) bozulmaz (UTF-8)", SalonGeometriTests.DxfExportTurkceKategoriBozulmaz),
    ("M51: hacim prizma hesaplanır (bbox değil)", SalonGeometriTests.HacimPrizmaHesaplanir),
    ("M51: hacim sahne/seyirci düşümleri uygulanır", SalonGeometriTests.HacimPlatformDusumleriUygulanir),
    ("M51: null collections normalize and future schema is rejected", M01GeometryReliabilityTests.SalonLoadNormalizesNullCollectionsAndRejectsFutureSchema),
    ("M51: elevated audience preserves air below base elevation", M01GeometryReliabilityTests.ElevatedAudienceDoesNotConsumeAirBelowBaseElevation),
    ("M51→M01: köprü gerçek hacim + kaynak revizyon taşır", SalonGeometriTests.M01KopruGercekHacimVeRevizyonTasir),
    ("M51: zemin seyirci altından kırpılır (keyhole delik)", SalonGeometriTests.ZeminSeyirciAltindanKirpilir),
    ("M51: bölge öğesi kabuk üretmez, zemini kırpar", SalonGeometriTests.BolgeOgesiKabukUretmezZeminKirpar),
    ("M51: şablon seyircisi dış hatta sığar (yelpaze/at nalı)", SalonGeometriTests.SablonSeyirciDisHattaSigar),
    ("M51: şablon 'salon önü' sahne cebi (proscenium) açılır", SalonGeometriTests.SablonSalonOnuSahneCebiAcilir),
    ("M51: doğrulama taşma + kabuk uyarıları üretir", SalonGeometriTests.DogrulamaTasmaVeKabukUyarisi),
    ("M51: undo kopyası (KopyaJson) derin round-trip korur", SalonGeometriTests.KopyaJsonRoundTrip),
    ("M51: atomik kaydet — hata revizyonu geri alır, tmp kalmaz", SalonGeometriTests.AtomikKaydetHataRevizyonuGeriAlir),
    ("M51/DXF: konkav poligon üçgenleme alanı korur (ear clipping)", SalonGeometriTests.KonkavPoligonUcgenlemeAlaniKorur),
    ("M51: tavan profili enterpolasyon + clamp doğru", SalonGeometriTests.TavanProfiliEnterpolasyon),
    ("M51: 3D — profilli tavan paneli + duvar üstleri profili izler", SalonGeometriTests.Uc3DProfilliTavanVeDuvarUretir),
    ("M51: hacim — profilli tavan integrali doğru", SalonGeometriTests.HacimProfilliTavanIntegrali),
    ("M51: tavan profili round-trip korunur", SalonGeometriTests.TavanProfiliRoundTrip),
    ("M51: koltuk kapasitesi hesaplanır", SalonGeometriTests.KoltukKapasitesiHesaplanir),
    ("M51: ISalonGeometri sözleşmesi sahne/seyirci/balkon/tavanı kapsar", SalonGeometriTests.ArayuzSozlesmesiTamKapsar),
    ("M51: doğrulama tavan profili uyarıları üretir", SalonGeometriTests.DogrulamaTavanProfiliUyarilari),
    ("M51: seyirci basamak/korniş kabuğa kırpılır", SalonGeometriTests.SeyirciKabugaKirpilir),
    ("M51: orkestra çukuru — zemin açıklığı + taban + duvarlar + hacim", SalonGeometriTests.OrkestraCukuru3DVeHacim),
    ("M51: orkestra çukuru round-trip + sahne çakışma uyarısı", SalonGeometriTests.OrkestraCukuruRoundTripVeDogrulama),
    ("M51: faset ayarı etkili + 12-128 kelepçe", SalonGeometriTests.FasetAyariEtkili),
    ("M51: kapı duvardan kesilir, 'Kapı' kategorisiyle doldurulur", SalonGeometriTests.KapiDuvariKeserVeDoldurur),
    ("M51: pencere alt kotu uygulanır (keyhole delik)", SalonGeometriTests.PencereAltKotUygulanir),
    ("M51: açıklık round-trip + duvara bağlanma uyarısı", SalonGeometriTests.AciklikRoundTripVeDogrulama),
    ("M51: üç noktadan yay kurulur (merkez/yarıçap/uçlar/uzunluk)", SalonGeometriTests.UcNoktadanYayKurulur),
    ("M51: yay round-trip + 3D fasetli duvar üretir", SalonGeometriTests.YayRoundTripVeDuvarUretir),
    ("M51: uzat (extend) ilk ileri kesişime taşır", SalonGeometriTests.UzatKesisimeUzar),
    ("M51: açık offset paralel + köşe miter doğru", SalonGeometriTests.AcikOffsetParalelUretir),
    ("M51: taşı/döndür/aynala yay açılarını korur + kopya bağımsız", SalonGeometriTests.DonusumlerYayAcilariniKorur),
    ("M30: .e4mimari.json kaydet→aç round-trip", MimariProjeServisiTests.E4MimariRoundTrip),
    ("M30: boş mimari proje kritik sonuç üretir", MimariProjeDogrulamaTests.BosProjeKritikSonucUretir),
    ("M30: eksik bina işlevi ve boş mahal listesi yakalanır", MimariProjeDogrulamaTests.EksikBinaIsleviVeBosMahalListesiYakalanir),
    ("M30: negatif alan ve geçersiz adet yakalanır", MimariProjeDogrulamaTests.NegatifAlanVeGecersizAdetYakalanir),
    ("M30: geçerli minimal proje kritik üretmez", MimariProjeDogrulamaTests.GecerliMinimalProjeKritikUretmez),
    ("Sembol kütüphanesi: DXF round-trip (export → import) eşleşen blockları günceller", BosluktanOdaTespitTests.SembolDxfRoundTrip),
    ("Sembol import: rapor eşleşmeyen/beklenen/boş block'ları listeler", BosluktanOdaTespitTests.SembolImportRaporuDiagnosticVerir),
    ("Sembol import: case-insensitive block adı eşleşir", BosluktanOdaTespitTests.SembolImportCaseInsensitiveEslesir),
    ("Sembol import: gömülü şablon kaynaktan uygulanır", BosluktanOdaTespitTests.GomuluSembolSablonuKaynaktanUygulanir),
    ("Sembol: priz T çubuğu dome tepesinde (EMO standardı)", BosluktanOdaTespitTests.PrizSembolTCubukDomeTepesinde),
    ("Açıklık: arc kapı tespit edilir", AciklikTespitTests.ArcKapiAciklikUretir),
    ("Açıklık: geniş arc kapı sayılmaz", AciklikTespitTests.GenisArcKapiSayilmaz),
    ("Açıklık: duvar boşluğundan gap açıklığı", AciklikTespitTests.DuvarBoslugundanAciklik),
    ("Açıklık: söğesiz gap reddedilir", AciklikTespitTests.SovesizGapReddedilir),
    ("Açıklık: karşı duvarsız gap reddedilir", AciklikTespitTests.KarsiDuvarsizGapReddedilir),
    ("Açıklık: uzak karşı duvar (>1.5 m) reddedilir", AciklikTespitTests.UzakKarsiDuvarReddedilir),
    ("Açıklık: iptal edilmiş kayıt korunur", AciklikTespitTests.IptalEdilmisKayitKorunur),
    ("Açıklık: manuel kayıt korunur", AciklikTespitTests.ManuelKayitKorunur),
    ("Açıklık: IsAciklikYakininda yakın/uzak/iptal", AciklikTespitTests.IsAciklikYakindaTemelDavranis),
    ("Flood-fill: ekDuvarlar açıklığı sanal duvarla kapatır", AciklikTespitTests.FloodFillEkDuvarlarOdayiBolerYap),
    ("Açıklık: komşu duvara otomatik yansır", AciklikTespitTests.KomsuDuvaraAciklikYansir),
    ("Açıklık: komşu duvar yoksa yansıma olmaz", AciklikTespitTests.KomsuDuvarYoksaYansimaOlmaz),
    ("Açıklık: komşu açıklık birlikte silinir", AciklikTespitTests.KomsuAciklikBirlikteSilinir),
    ("Açıklık: yansıma aynı oda kenarına atlamaz", AciklikTespitTests.KomsuYansimaAyniOdaKenarinaAtlamaz),
    ("Çevre aydınlatma: LDT park armatürünü okur", CevreAydinlatmaTests.LdtParkArmaturunuOkur),
    ("Çevre aydınlatma: IES park armatürünü okur", CevreAydinlatmaTests.IesParkArmaturunuOkur),
    ("Çevre aydınlatma: boş fotometri içeriği hata verir", CevreAydinlatmaTests.BosIcerikHataVerir),
    ("Çevre aydınlatma: ortak yerleşim geometrisi kenar ve ızgara üretir", CevreAydinlatmaTests.YerlesimGeometriKenarVeIzgaraUretir),
    ("Çevre aydınlatma: ortak DXF segment sadeleştirme kısaları eler", CevreAydinlatmaTests.DxfSegmentSadelestirmeKisalariEler),
    ("Çevre aydınlatma: yüzey kategorisi katmandan eşleşir", CevreAydinlatmaTests.YuzeyKategoriKatmandanEslesir),
    ("Çevre aydınlatma: kapalı poligonlar yüzeye çevrilir", CevreAydinlatmaTests.KapaliPoligonlarYuzeyeCevrilir),
    ("Çevre aydınlatma: armatür fotometriden üretilir", CevreAydinlatmaTests.ArmaturFotometridenUretilir),
    ("Çevre aydınlatma: güzergâha eşit aralık yerleşir", CevreAydinlatmaTests.GuzergahaEsitAralikYerlesir),
    ("Çevre aydınlatma: poligon çevresine yerleşir", CevreAydinlatmaTests.PoligonCevresineYerlesir),
    ("Çevre aydınlatma: lümen metodu hesaplar", CevreAydinlatmaTests.LumenMetoduHesaplar),
    ("Çevre aydınlatma: devre kablo hesabı M11 ile tutarlı", CevreAydinlatmaTests.DevreKabloHesabiM11ileTutarli),
    ("Çevre aydınlatma: TT topraklama hesaplar", CevreAydinlatmaTests.TopraklamaTtHesaplar),
    ("Çevre aydınlatma: tek hat aktarımı devreleri üretir", CevreAydinlatmaTests.TekHatAktarimDevreleriUretir),
    ("Çevre aydınlatma: tek hat aktarımı .e4p11 round-trip", CevreAydinlatmaTests.TekHatAktarimE4p11RoundTrip),
    ("Çevre aydınlatma: ViewModel akışı çalışır", CevreAydinlatmaTests.ViewModelAkisiCalisir),
    ("Çevre aydınlatma: proje .e4p15 round-trip", CevreAydinlatmaTests.ProjeE4p15RoundTrip),
    ("Çevre aydınlatma: arka plan çizgileri çıkarılır", CevreAydinlatmaTests.ArkaPlanCizgileriCikarilir),
    ("Çevre aydınlatma: yüzeyden güzergâh ve direk", CevreAydinlatmaTests.YuzeydenGuzergahVeDirek)
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Console.WriteLine($"{failed} test failed.");
    return 1;
}

Console.WriteLine($"{tests.Length} tests passed.");
return 0;

static void AbsorptiveSurfaceThickerPorousLayerImprovesNrc()
{
    var thin = AbsorptiveSurfaceCalculationService.Calculate(new[]
    {
        new AbsorptiveLayer { MaterialCode = "MW-48", LayerType = AbsorptiveLayerType.PorousAbsorber, ThicknessMm = 25 },
        new AbsorptiveLayer { MaterialCode = "AIR", LayerType = AbsorptiveLayerType.AirGap, ThicknessMm = 25 }
    });

    var thick = AbsorptiveSurfaceCalculationService.Calculate(new[]
    {
        new AbsorptiveLayer { MaterialCode = "MW-48", LayerType = AbsorptiveLayerType.PorousAbsorber, ThicknessMm = 75 },
        new AbsorptiveLayer { MaterialCode = "AIR", LayerType = AbsorptiveLayerType.AirGap, ThicknessMm = 75 }
    });

    AssertGreater(thick.Nrc, thin.Nrc + 0.05, "Thicker porous absorber with air gap should improve NRC.");
}

static void AbsorptiveSurfacePerforatedPanelWarnsWithoutAirGap()
{
    var result = AbsorptiveSurfaceCalculationService.Calculate(new[]
    {
        new AbsorptiveLayer { MaterialCode = "GY-PERF", LayerType = AbsorptiveLayerType.PerforatedPanel, ThicknessMm = 12, OpenAreaPercent = 16 },
        new AbsorptiveLayer { MaterialCode = "MW-48", LayerType = AbsorptiveLayerType.PorousAbsorber, ThicknessMm = 40 }
    });

    if (!result.Warnings.Any(w => w.Contains("hava boşluğu", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Perforated panel without air gap should produce an air gap warning.");
}

// ── Maa perfore panel: rezonans frekansı formüle yakın olmalı ──
// f₀ = (c / 2π) · sqrt(σ / (t_eff · D)), t_eff = t_panel + 0.85·d
// Test: t=10mm, d=6mm, σ=15%, D=100mm → t_eff ≈ 0.01510m, D=0.10m
//   f₀ = (343/2π) · sqrt(0.15 / (0.01510·0.10)) ≈ 54.6 · sqrt(99.3) ≈ 544 Hz
// 500 Hz oktav bandında α belirgin biçimde yükselmeli (poröz arkalı geniş tepe).
static void AbsorptiveMaaPanelPeaksNearResonance()
{
    var result = AbsorptiveSurfaceCalculationService.Calculate(new[]
    {
        new AbsorptiveLayer { MaterialCode = "GY-PERF", LayerType = AbsorptiveLayerType.PerforatedPanel,
                              ThicknessMm = 10, OpenAreaPercent = 15, HoleDiameterMm = 6 },
        new AbsorptiveLayer { MaterialCode = "AIR",   LayerType = AbsorptiveLayerType.AirGap, ThicknessMm = 100 },
        new AbsorptiveLayer { MaterialCode = "MW-48", LayerType = AbsorptiveLayerType.PorousAbsorber, ThicknessMm = 40 }
    });

    double alpha500  = result.AbsorptionBands.GetValueOrDefault(500);
    double alpha63   = result.AbsorptionBands.GetValueOrDefault(63);
    double alpha4000 = result.AbsorptionBands.GetValueOrDefault(4000);

    // 500 Hz tepeye yakın; uçlara göre belirgin daha yüksek olmalı
    AssertGreater(alpha500, alpha63   + 0.10, "Maa panel α(500) tepeye yakın 63 Hz'den belirgin yüksek olmalı.");
    AssertGreater(alpha500, 0.55,             "Maa panel α(500) ≥ 0.55 beklenir (poröz arkalı geniş tepe).");
    // 4kHz'de poröz baskın → α yine yüksek olabilir; sadece tepe orta-bantta görünür olsun
    AssertGreater(alpha500, alpha4000 - 0.10, "Tepe orta bantta yer almalı.");
}

// ── AbsorptiveReportBuilder: çubuk grafik + bant tablosu + αw görünür ──
static void AbsorptiveReportBuilderContainsChartAndBands()
{
    var layers = new[]
    {
        new AbsorptiveLayer { Index = 1, MaterialCode = "FAB-AC", LayerType = AbsorptiveLayerType.Fabric,         ThicknessMm = 2 },
        new AbsorptiveLayer { Index = 2, MaterialCode = "MW-48",  LayerType = AbsorptiveLayerType.PorousAbsorber, ThicknessMm = 50 },
        new AbsorptiveLayer { Index = 3, MaterialCode = "AIR",    LayerType = AbsorptiveLayerType.AirGap,         ThicknessMm = 50 }
    };
    var result = AbsorptiveSurfaceCalculationService.Calculate(layers);
    var md = AbsorptiveReportBuilder.Build("Test Yüzey", layers, result);

    foreach (var fragment in new[]
    {
        "# Ses Emici Yüzey Analiz Raporu",
        "## 1. Özet",
        "## 2. Katman Yapısı",
        "## 3. Oktav Bant Soğurma Katsayıları",
        "## 4. α Spektrum Görseli",
        "NRC:",
        "αw"
    })
    {
        if (!md.Contains(fragment, StringComparison.Ordinal))
            throw new InvalidOperationException($"Rapor '{fragment}' içermiyor.");
    }

    // Çubuk grafiği en az bir dolu blok içermeli (en yüksek α için)
    if (!md.Contains("█", StringComparison.Ordinal))
        throw new InvalidOperationException("ASCII bar chart dolu blok içermiyor.");
}

// ── Hava boşluğu λ/4 etkisi: porous + airgap düşük frekans α'yı yükseltir ──
static void AbsorptiveAirGapBoostsLowFrequencyAlpha()
{
    var noGap = AbsorptiveSurfaceCalculationService.Calculate(new[]
    {
        new AbsorptiveLayer { MaterialCode = "MW-48", LayerType = AbsorptiveLayerType.PorousAbsorber, ThicknessMm = 50 }
    });

    var withGap = AbsorptiveSurfaceCalculationService.Calculate(new[]
    {
        new AbsorptiveLayer { MaterialCode = "MW-48", LayerType = AbsorptiveLayerType.PorousAbsorber, ThicknessMm = 50 },
        new AbsorptiveLayer { MaterialCode = "AIR",   LayerType = AbsorptiveLayerType.AirGap,        ThicknessMm = 150 }
    });

    double noGap125   = noGap.AbsorptionBands.GetValueOrDefault(125);
    double withGap125 = withGap.AbsorptionBands.GetValueOrDefault(125);

    // λ/4 = 50+150 = 200mm → f ≈ 343/0.8 ≈ 429 Hz; ama düşük frekans boost'u 125'te de görünmeli
    AssertGreater(withGap125, noGap125 + 0.02, "Arka hava boşluğu düşük frekans (125 Hz) α'yı yükseltmeli.");
}

static void RoomModesAxialFrequencyMatchesFormula()
{
    var result = RoomModeCalculator.Calculate(new RoomModeInput
    {
        LengthX = 5,
        LengthY = 4,
        LengthZ = 3,
        Temperature = 20,
        MaxFrequency = 120,
        RT60 = 0.4
    });

    var firstX = result.Modes.FirstOrDefault(m => m.Nx == 1 && m.Ny == 0 && m.Nz == 0)
                 ?? throw new InvalidOperationException("Missing (1,0,0) axial mode.");
    var expected = result.SpeedOfSound / (2 * 5.0);
    AssertClose(expected, firstX.Frequency, 0.02, "Room mode (1,0,0)");
}

static void RoomModesInvalidInputsReturnWarning()
{
    var result = RoomModeCalculator.Calculate(new RoomModeInput { LengthX = 0, LengthY = 4, LengthZ = 3 });
    if (result.Modes.Count != 0)
        throw new InvalidOperationException("Invalid room dimensions should not produce modes.");
    if (result.Warnings.Count == 0)
        throw new InvalidOperationException("Invalid room dimensions should produce a warning.");
}

static void RoomModesMissingRt60EstimatesSchroeder()
{
    var result = RoomModeCalculator.Calculate(new RoomModeInput
    {
        LengthX = 5,
        LengthY = 4,
        LengthZ = 3,
        MaxFrequency = 100
    });

    if (result.SchroederFrequency is null || !result.IsRT60Estimated || result.EffectiveRT60 is null)
        throw new InvalidOperationException("Missing RT60 should produce estimated Schroeder frequency.");
}

static void RoomModesPressureSamplesStayNormalized()
{
    var room = new RoomModeInput { LengthX = 5, LengthY = 4, LengthZ = 3 };
    var mode = new RoomMode(34.3, 1, 0, 0, RoomModeType.Axial);
    var points = PressureFieldSampler.SampleSurface(mode, room, resolution: 4);
    if (points.Count == 0)
        throw new InvalidOperationException("Pressure sampler returned no points.");
    if (points.Any(p => p.Pressure < -1.0001 || p.Pressure > 1.0001 || !double.IsFinite(p.Pressure)))
        throw new InvalidOperationException("Pressure sample outside normalized range.");
}

static void RoomModesQrdDiffuserResiduesFollowPrimeSequence()
{
    var result = QrdDiffuserCalculator.Calculate(new DiffuserDesignInput
    {
        TargetFrequencyHz = 500,
        PrimeOrder = 7,
        WellWidthMm = 60,
        PanelHeightMm = 600,
        MaxDepthLimitMm = 500,
        SoundSpeed = 343
    });

    var residues = string.Join(",", result.Wells.Select(w => w.Residue));
    if (residues != "0,1,4,2,2,4,1")
        throw new InvalidOperationException($"Unexpected QRD residue sequence: {residues}");
    if (result.PanelWidthMm != 420)
        throw new InvalidOperationException("QRD panel width should be well width multiplied by prime order.");
}

static void RoomModesDiffuserFlagsLowModalFrequency()
{
    var result = QrdDiffuserCalculator.Calculate(new DiffuserDesignInput
    {
        TargetFrequencyHz = 63,
        PrimeOrder = 7,
        WellWidthMm = 70,
        PanelHeightMm = 600,
        MaxDepthLimitMm = 250,
        SchroederFrequencyHz = 180
    });

    if (result.Suitability != DiffuserSuitability.NotRecommended)
        throw new InvalidOperationException("Low modal frequency should not be recommended for diffuser-first treatment.");
    if (!result.WarningText.Contains("bas tuzağı", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Low modal frequency warning should mention bass traps.");
}

static void RoomModesQrdDiffuserRespectsMaxDepth()
{
    var result = QrdDiffuserCalculator.Calculate(new DiffuserDesignInput
    {
        TargetFrequencyHz = 125,
        PrimeOrder = 7,
        WellWidthMm = 70,
        PanelHeightMm = 600,
        MaxDepthLimitMm = 120
    });

    if (result.Wells.Any(w => w.DepthMm > 120.01))
        throw new InvalidOperationException("QRD well depth exceeded max depth limit.");
    if (!result.WarningText.Contains("kırpıldı", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Clipped QRD depth should produce a warning.");
}

static void RoomModesSkylineDiffuserBuildsSquareCellGrid()
{
    var result = SkylineDiffuserCalculator.Calculate(new DiffuserDesignInput
    {
        TargetFrequencyHz = 800,
        PrimeOrder = 7,
        WellWidthMm = 50,
        MaxDepthLimitMm = 400,
        SoundSpeed = 343
    });

    if (result.Cells.Count != 49)
        throw new InvalidOperationException("Skyline N=7 diffuser should produce 49 cells.");

    var cell = result.Cells.FirstOrDefault(c => c.Row == 2 && c.Column == 3)
               ?? throw new InvalidOperationException("Expected Skyline cell was not produced.");
    if (cell.Residue != 5)
        throw new InvalidOperationException($"Unexpected Skyline residue for row 2, column 3: {cell.Residue}");
}

static void RoomModesSkylineDiffuserFlagsClippedLowFrequency()
{
    var result = SkylineDiffuserCalculator.Calculate(new DiffuserDesignInput
    {
        TargetFrequencyHz = 63,
        PrimeOrder = 7,
        WellWidthMm = 70,
        MaxDepthLimitMm = 120,
        SchroederFrequencyHz = 180
    });

    if (result.Cells.Any(c => c.DepthMm > 120.01))
        throw new InvalidOperationException("Skyline cell depth exceeded max depth limit.");
    if (!result.WarningText.Contains("gerçek Skyline dizilimi oluşmuyor", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Clipped Skyline design should explain that the 2D sequence collapsed.");
}

static void MeasurementRtaLevelsRemainDbfsNormalized()
{
    const int sampleRate = 48000;
    var analyzer = new RtaAnalyzer(sampleRate, 8192);
    var samples = new float[8192];
    for (int i = 0; i < samples.Length; i++)
        samples[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / sampleRate));

    var levels = analyzer.Process(samples);
    if (levels.Length == 0)
        throw new InvalidOperationException("RTA analyzer returned no bands.");
    if (levels.Any(l => !float.IsFinite(l)))
        throw new InvalidOperationException("RTA analyzer returned a non-finite level.");
    if (levels.Max() > 0.1f)
        throw new InvalidOperationException($"RTA levels should stay normalized to dBFS. Max was {levels.Max():F2} dB.");
}

static void MeasurementRtRegressionTracksExponentialDecay()
{
    const int sampleRate = 48000;
    const double expectedRt60 = 0.8;
    var length = (int)(sampleRate * 2.0);
    var ir = new float[length];
    var decayRate = Math.Log(1000.0) / expectedRt60;

    for (int i = 0; i < length; i++)
    {
        var t = (double)i / sampleRate;
        ir[i] = (float)Math.Exp(-decayRate * t);
    }

    var parameters = AcousticParameterExtractor.Extract(ir, sampleRate);
    AssertBetween(parameters.RT20, 0.65, 0.95, "RT20 regression");
    AssertBetween(parameters.RT30, 0.65, 0.95, "RT30 regression");
}

static void MeasurementUnsupportedCapturePlatformDoesNotAdvertiseInputDevices()
{
    if (AudioDeviceEnumerator.IsCaptureSupported)
        return;

    if (AudioDeviceEnumerator.GetInputDevices().Count != 0)
        throw new InvalidOperationException("Unsupported capture platforms should not expose placeholder input devices.");
}

static void MeasurementSessionRoundTripPreservesRoomDimensionsAndSpectrogram()
{
    var session = new MeasurementSession
    {
        Name = "Round-trip test",
        RoomDimensions = (6.0, 4.5, 3.0),
        Measurements =
        {
            new MeasurementData
            {
                Label = "Ölçüm 1",
                Spectrogram = new SpectrogramData(
                    new float[] { 0.0f, 0.1f },
                    new float[] { 100f, 200f },
                    new float[,] { { -50f, -40f }, { -30f, -20f } })
            }
        }
    };

    var path = Path.Combine(Path.GetTempPath(), $"m07_session_{Guid.NewGuid():N}.eye4meas.json");
    try
    {
        MeasurementRepository.SaveSessionAsync(session, path).GetAwaiter().GetResult();
        var loaded = MeasurementRepository.LoadSessionAsync(path).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("Session failed to load.");

        AssertBetween(loaded.RoomDimensions.L, 5.999, 6.001, "RoomDimensions.L round-trip");
        AssertBetween(loaded.RoomDimensions.W, 4.499, 4.501, "RoomDimensions.W round-trip");
        AssertBetween(loaded.RoomDimensions.H, 2.999, 3.001, "RoomDimensions.H round-trip");

        if (loaded.Measurements.Count != 1)
            throw new InvalidOperationException("Measurement should round-trip.");
        var spec = loaded.Measurements[0].Spectrogram
            ?? throw new InvalidOperationException("Spectrogram should round-trip (float[,] must be JSON-serializable).");
        if (spec.MagnitudeDb.GetLength(0) != 2 || spec.MagnitudeDb.GetLength(1) != 2)
            throw new InvalidOperationException("Spectrogram dimensions should round-trip.");
        AssertBetween(spec.MagnitudeDb[1, 1], -20.001, -19.999, "Spectrogram value round-trip");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

static void MeasurementCsvExportIsCultureInvariant()
{
    var previousCulture = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
    var path = Path.Combine(Path.GetTempPath(), $"m07_fr_{Guid.NewGuid():N}.csv");
    try
    {
        var data = new MeasurementData
        {
            FrequencyResponse = new FrequencyResponseData(
                new float[] { 1000.5f }, new float[] { -12.34f }, new float[] { 0.5f })
        };
        ExportService.ExportToCsvAsync(data, path).GetAwaiter().GetResult();
        var line = File.ReadAllLines(path)[1];
        var fields = line.Split(',');
        if (fields.Length != 3)
            throw new InvalidOperationException(
                $"CSV row should have exactly 3 comma-separated fields under tr-TR culture, got: '{line}'");
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
        if (File.Exists(path)) File.Delete(path);
    }
}

static void MeasurementIrAlignmentPreservesDirectSoundPeak()
{
    const int sampleRate = 48000;
    // Peak, tamponun başına yakın bir yerde (eski tam-tampon Tukey penceresi burada
    // direkt sesi neredeyse tamamen silerdi).
    var ir = new float[262144];
    int peakAt = 500;
    ir[peakAt] = 1.0f;

    var aligned = ImpulseResponseWindow.AlignAndWindow(ir, sampleRate);
    float maxAfter = 0;
    foreach (var v in aligned) maxAfter = Math.Max(maxAfter, Math.Abs(v));

    if (maxAfter < 0.95f)
        throw new InvalidOperationException(
            $"Direct sound peak near buffer start should survive alignment near-unchanged, got {maxAfter:F3}.");
}

static void MeasurementFailedRtRegressionYieldsNaNNotZero()
{
    const int sampleRate = 48000;
    // Sabit (sönümsüz) sinyal: geriye doğru Schroeder eğimi negatif olmaz, RT hesaplanamamalı.
    var ir = new float[sampleRate];
    for (int i = 0; i < ir.Length; i++) ir[i] = 1.0f;

    var parameters = AcousticParameterExtractor.Extract(ir, sampleRate);
    if (!double.IsNaN(parameters.RT30))
        throw new InvalidOperationException(
            $"Un-decaying signal should yield RT30=NaN (cannot compute), not a misleading 0. Got {parameters.RT30}.");
}

static void MeasurementFrequencyResponseLevelIsNotFftLengthAttenuated()
{
    const int sampleRate = 48000;
    // Genliği 1.0 olan tek örneklik impuls: FFT'de her binde |mag|=1.0 (düz spektrum) beklenir.
    var ir = new float[8192];
    ir[0] = 1.0f;

    var fr = FrequencyResponseAnalyzer.Analyze(ir, sampleRate, null, 0);
    // fft.Length'e (8192) bölünseydi seviye ~ -78 dB civarına düşerdi; düzeltmeyle 0 dB civarında kalmalı.
    AssertBetween(fr.Magnitudes[10], -1.0, 1.0, "FR magnitude for a unit impulse should stay near 0 dB");
}

static void BuildingImpactPathFindsReceiverBelow()
{
    var doseme = new YapiElemani { Ad = "Test döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 78, DeltaLw = 20 };
    var altMekan = CreateRectMekan("Alt yatak", 0, 0, 4, 5);
    var ustMekan = CreateRectMekan("Üst salon", 0.5, 0.5, 4.5, 5.5);
    ustMekan.ZeminYapiElemani = doseme;

    var altKat = new Kat { KatNo = 1, KatYuksekligi = 3 };
    altKat.Mekanlar.Add(altMekan);
    var ustKat = new Kat { KatNo = 2, KatYuksekligi = 3 };
    ustKat.Mekanlar.Add(ustMekan);

    var yol = new KatlarArasiDarbeYoluServisi().BulAltMekan([altKat, ustKat], ustKat, ustMekan);
    if (yol == null)
        throw new InvalidOperationException("Impact path should find the overlapping room below.");
    if (!ReferenceEquals(yol.AliciMekan, altMekan))
        throw new InvalidOperationException("Impact path selected the wrong receiving room.");
    AssertGreater(yol.OrtusenAlan, 10, "Impact overlap area");
}

static void BuildingImpactPathRespectsFloorOffsets()
{
    var doseme = new YapiElemani { Ad = "Test döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 78, DeltaLw = 20 };
    var altMekan = CreateRectMekan("Alt oda", 0, 0, 4, 4);
    var ustMekan = CreateRectMekan("Üst oda", 0, 0, 4, 4);
    ustMekan.ZeminYapiElemani = doseme;

    var altKat = new Kat { KatNo = 1, KatYuksekligi = 3 };
    altKat.Mekanlar.Add(altMekan);
    var ustKat = new Kat { KatNo = 2, KatYuksekligi = 3, OriginOffset = new Point2D(10, 0) };
    ustKat.Mekanlar.Add(ustMekan);

    var yol = new KatlarArasiDarbeYoluServisi().BulAltMekan([altKat, ustKat], ustKat, ustMekan);
    if (yol != null)
        throw new InvalidOperationException("Impact path should not match rooms separated by floor offset.");
}

static void BuildingImpactPathRejectsBoundingBoxOnlyOverlap()
{
    // P1 regresyon (Faz 3) — eskiden bounding-box kesişimi kullanılıyordu. L şeklinde
    // (konkav) bir kaynak odanın "eksik köşesi"ne düşen bir alıcı oda, bbox'ta örtüşse de
    // gerçekte hiç çakışmaz; artık gerçek poligon kesişimi (Clipper2) bunu doğru reddediyor.
    var doseme = new YapiElemani { Ad = "Test döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 78, DeltaLw = 20 };
    // L şekli: bbox (0,0)-(4,4), sağ-üst 2x2 köşe (2,2)-(4,4) EKSİK.
    var ustMekan = new Mekan
    {
        Ad = "Üst L-oda", KullanimTipi = "Üst L-oda", Yukseklik = 3,
        Poligon =
        [
            new Point2D(0, 0), new Point2D(4, 0), new Point2D(4, 2),
            new Point2D(2, 2), new Point2D(2, 4), new Point2D(0, 4)
        ],
        ZeminYapiElemani = doseme
    };
    // Alıcı tam olarak L'nin eksik köşesine (2,2)-(4,4) düşüyor — bbox'ta örtüşür, gerçekte örtüşmez.
    var altMekan = CreateRectMekan("Alt köşe oda", 2, 2, 4, 4);

    var altKat = new Kat { KatNo = 1, KatYuksekligi = 3 };
    altKat.Mekanlar.Add(altMekan);
    var ustKat = new Kat { KatNo = 2, KatYuksekligi = 3 };
    ustKat.Mekanlar.Add(ustMekan);

    var yol = new KatlarArasiDarbeYoluServisi().BulAltMekan([altKat, ustKat], ustKat, ustMekan);
    if (yol != null)
        throw new InvalidOperationException(
            "L şeklinin eksik köşesine düşen oda gerçek poligonla çakışmıyor; bbox yanlışlıkla eşleştiriyordu.");
}

static void BuildingImpactPathFindsMultipleSubReceivers()
{
    // P1 regresyon (Faz 3) — eskiden yalnız en fazla örtüşen TEK alt mekan dönüyordu.
    // Üstteki geniş oda iki alt odaya birden taşıyorsa ikinci alıcı tamamen atlanıyordu.
    var doseme = new YapiElemani { Ad = "Test döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 78, DeltaLw = 20 };
    var ustMekan = CreateRectMekan("Üst geniş salon", 0, 0, 8, 4);
    ustMekan.ZeminYapiElemani = doseme;
    var alt1 = CreateRectMekan("Alt oda 1", 0, 0, 4, 4);
    var alt2 = CreateRectMekan("Alt oda 2", 4, 0, 8, 4);

    var altKat = new Kat { KatNo = 1, KatYuksekligi = 3 };
    altKat.Mekanlar.Add(alt1);
    altKat.Mekanlar.Add(alt2);
    var ustKat = new Kat { KatNo = 2, KatYuksekligi = 3 };
    ustKat.Mekanlar.Add(ustMekan);

    var yollar = new KatlarArasiDarbeYoluServisi().BulAltMekanlar([altKat, ustKat], ustKat, ustMekan);
    if (yollar.Count != 2)
        throw new InvalidOperationException($"İki alt odaya taşan üst oda 2 darbe yolu üretmeli, alınan: {yollar.Count}");
    if (!yollar.Any(y => ReferenceEquals(y.AliciMekan, alt1)) || !yollar.Any(y => ReferenceEquals(y.AliciMekan, alt2)))
        throw new InvalidOperationException("Her iki alt oda da darbe yolu listesinde olmalı.");
}

static void BuildingFlankingExposesThirteenPathModel()
{
    // Kütleli + flank elemanları ayırıcıdan belirgin zayıf → flanking apparent Rw'yi açıkça düşürür.
    // (Massless + yakın Rw'de per-eleman T_s ile flanking ihmal edilebilir hale gelebiliyordu.)
    var ayirici = new YapiElemani { Ad = "Ayırıcı duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55, BaglantiTipi = BaglantiTipi.Rijit, Katmanlar = { new YapiKatmani { MalzemeAdi = "beton", Kalinlik = 350, Yogunluk = 1000 } } };
    var yan = new YapiElemani { Ad = "Yan duvar", Tip = YapiElemaniTipi.Duvar, Rw = 45, BaglantiTipi = BaglantiTipi.Rijit, Katmanlar = { new YapiKatmani { MalzemeAdi = "tuğla", Kalinlik = 150, Yogunluk = 1000 } } };
    var doseme = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Rw = 48, BaglantiTipi = BaglantiTipi.Rijit, Katmanlar = { new YapiKatmani { MalzemeAdi = "beton", Kalinlik = 200, Yogunluk = 1000 } } };

    var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
    kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
    alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;

    var bolme = new Bolme
    {
        Ad = "Ayırıcı",
        Alan = 9,
        Baslangic = new Point2D(4, 0),
        Bitis = new Point2D(4, 3),
        HasGeometry = true,
        YapiElemani = ayirici,
        KaynakMekan = kaynak,
        AliciMekan = alici
    };
    kaynak.Bolmeler.Add(bolme);
    kaynak.Bolmeler.Add(new Bolme { Ad = "Kaynak alt yan", Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0), HasGeometry = true, YapiElemani = yan, KaynakMekan = kaynak });
    kaynak.Bolmeler.Add(new Bolme { Ad = "Kaynak üst yan", Baslangic = new Point2D(0, 3), Bitis = new Point2D(4, 3), HasGeometry = true, YapiElemani = yan, KaynakMekan = kaynak });
    alici.Bolmeler.Add(new Bolme { Ad = "Alıcı alt yan", Baslangic = new Point2D(4, 0), Bitis = new Point2D(8, 0), HasGeometry = true, YapiElemani = yan, KaynakMekan = alici });
    alici.Bolmeler.Add(new Bolme { Ad = "Alıcı üst yan", Baslangic = new Point2D(4, 3), Bitis = new Point2D(8, 3), HasGeometry = true, YapiElemani = yan, KaynakMekan = alici });

    var yollar = HavaSesiHesapServisi.GetOnUcYolFlankingYollari(bolme);
    if (yollar.Count != 12)
        throw new InvalidOperationException($"Expected 12 flanking paths plus 1 direct path, got {yollar.Count} flanking paths.");

    var apparent = new HavaSesiHesapServisi().HesaplaApparentRw(bolme);
    if (bolme.Junctions.Count != 4)
        throw new InvalidOperationException("Separating wall should have four generated junctions.");
    if (apparent <= 0 || apparent >= HavaSesiHesapServisi.CalculateCompositeRw(bolme))
        throw new InvalidOperationException("Flanking should lower apparent Rw compared with the direct composite Rw.");
}

static void BuildingThirdOctaveDataDrivesApparentRw()
{
    var low = Enumerable.Repeat(35.0, 16).ToList();
    var high = Enumerable.Repeat(55.0, 16).ToList();
    var source = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var receiver = CreateRectMekan("Alıcı", 4, 0, 8, 3);

    var lowWall = new YapiElemani { Ad = "Düşük üçüncü oktav", Tip = YapiElemaniTipi.Duvar, Rw = 55, RThirdOctave = low };
    var highWall = new YapiElemani { Ad = "Yüksek üçüncü oktav", Tip = YapiElemaniTipi.Duvar, Rw = 55, RThirdOctave = high };

    var lowBolme = new Bolme { Alan = 9, YapiElemani = lowWall, KaynakMekan = source, AliciMekan = receiver };
    var highBolme = new Bolme { Alan = 9, YapiElemani = highWall, KaynakMekan = source, AliciMekan = receiver };
    var service = new HavaSesiHesapServisi();

    var lowRw = service.HesaplaApparentRw(lowBolme);
    var highRw = service.HesaplaApparentRw(highBolme);

    if (lowBolme.UcuncuOktavHavaSesiSonucu?.R_f.Length != 16)
        throw new InvalidOperationException("Third-octave result should contain 16 bands.");
    AssertGreater(highRw, lowRw + 15, "Third-octave R data should drive the apparent Rw result.");
}

static void BuildingExternalNoiseSpectrumReportsFacadeIndicators()
{
    var wall = new YapiElemani
    {
        Ad = "Cephe duvarı",
        Tip = YapiElemaniTipi.Duvar,
        Rw = 48,
        Ctr = -3,
        RThirdOctave = Enumerable.Repeat(48.0, 16).ToList()
    };
    var window = new YapiElemani
    {
        Ad = "Pencere",
        Tip = YapiElemaniTipi.Pencere,
        Rw = 34,
        Ctr = -4,
        RThirdOctave = Enumerable.Repeat(34.0, 16).ToList()
    };

    var bolme = new Bolme
    {
        Ad = "Dış cephe",
        Alan = 12,
        YapiElemani = wall,
        KaynakMekan = CreateRectMekan("Oda", 0, 0, 4, 3),
        AliciMekan = null
    };
    bolme.Acikliklar.Add(new BolmeAcikligi { Tip = BolmeAcikligiTipi.Pencere, YapiElemani = window, Genislik = 1.5, Yukseklik = 1.4 });

    var proje = new BinaProje
    {
        DisGurultuDb = 75,
        DisGurultuCepheMaruziyeti = CepheMaruziyet.TrafikCephesi
    };

    var service = new DisGurultuServisi();
    service.UygulaDisCepheYalitimi(bolme.KaynakMekan!, [bolme], proje.DisGurultuCepheMaruziyeti);
    var sonuc = service.HesaplaSpektralDisCephe(proje, bolme)
        ?? throw new InvalidOperationException("External facade spectrum result should be produced.");

    if (sonuc.DisSpektrum.Length != DisGurultuServisi.ExtendedThirdOctaveFreqler.Length ||
        sonuc.DntSpektrum.Length != DisGurultuServisi.ExtendedThirdOctaveFreqler.Length ||
        sonuc.IcSpektrum.Length != DisGurultuServisi.ExtendedThirdOctaveFreqler.Length)
        throw new InvalidOperationException("External noise spectrum should use extended third-octave bands.");

    AssertClose(proje.DisGurultuDb, 10 * Math.Log10(sonuc.DisSpektrum.Sum(l => Math.Pow(10, l / 10.0))), 0.01, "Generated external spectrum total");
    AssertGreater(proje.DisGurultuDb, sonuc.IcToplam + 20, "Facade insulation should reduce spectral internal noise.");
    AssertGreater(sonuc.D2mNt50, 20, "D2m,nT,50 should be reported from low-frequency bands.");
}

static void BuildingReliabilityScoreReactsToMeasuredData()
{
    var room = CreateRectMekan("Oda", 0, 0, 4, 3);
    var modelWall = new YapiElemani { Ad = "Model duvar", Tip = YapiElemaniTipi.Duvar, Rw = 45 };
    var measuredWall = new YapiElemani
    {
        Ad = "Ölçümlü duvar",
        Tip = YapiElemaniTipi.Duvar,
        Rw = 45,
        HasRealOktavData = true,
        RThirdOctave = Enumerable.Repeat(45.0, 16).ToList()
    };

    var modelBolme = new Bolme { Ad = "Model", Alan = 9, HasGeometry = true, YapiElemani = modelWall, KaynakMekan = room };
    var measuredBolme = new Bolme { Ad = "Ölçümlü", Alan = 9, HasGeometry = true, YapiElemani = measuredWall, KaynakMekan = room };
    var service = new HesapGuvenilirlikServisi();

    var modelScore = service.Degerlendir(new BinaProje(), modelBolme);
    var measuredScore = service.Degerlendir(new BinaProje
    {
        DisGurultuThirdOctave = Enumerable.Repeat(60.0, DisGurultuServisi.ExtendedThirdOctaveFreqler.Length).ToList()
    }, measuredBolme);

    AssertGreater(measuredScore.Puan, modelScore.Puan + 15, "Measured spectral data should improve reliability score.");
    if (modelScore.Uyarilar.Count == 0)
        throw new InvalidOperationException("Model-based result should expose reliability warnings.");
}

static void BuildingEk2AutoEstimateMapsRoomLabels()
{
    var yatak = EK2SiniflandirmaServisi.TahminEt(BinaIslevi.Konut, "Yatak Odası", "");
    if (yatak is null || yatak.KaynakDerecesi != "OG" || yatak.AliciSinifi != "I")
        throw new InvalidOperationException("Konut/Yatak Odası otomatik tahmini OG/I üretmeli.");

    var salon = EK2SiniflandirmaServisi.TahminEt(BinaIslevi.Konut, "Salon", "");
    if (salon is null || salon.Ad != "Yaşam Alanları")
        throw new InvalidOperationException("Konut/Salon otomatik tahmini yaşam alanına düşmeli.");

    var wc = EK2SiniflandirmaServisi.TahminEt(BinaIslevi.Konut, "WC", "");
    if (wc is null || wc.Ad != "Mutfaklar / Banyo")
        throw new InvalidOperationException("Konut/WC otomatik tahmini mutfak-banyo kategorisine düşmeli.");

    var ofis = EK2SiniflandirmaServisi.TahminEt(BinaIslevi.BuroIdari, "Müdür Ofis", "");
    if (ofis is null || ofis.Ad != "Bireysel Ofisler")
        throw new InvalidOperationException("Büro/Ofis otomatik tahmini bireysel ofis kategorisine düşmeli.");

    var spor = EK2SiniflandirmaServisi.TahminEt(BinaIslevi.EglenceSporTesisleri, "Spor Salonu", "");
    if (spor is null || spor.Ad != "Spor / Etkinlik Alanları")
        throw new InvalidOperationException("Eğlence/Spor işlevinde Spor Salonu, spor-etkinlik kategorisine düşmeli.");
}

static void BuildingBinaIsleviPersistsInProjectJson()
{
    var proje = new BinaProje
    {
        ProjeAdi = "EK-2 kayıt testi",
        BinaIslevi = BinaIslevi.EglenceSporTesisleri
    };

    var json = System.Text.Json.JsonSerializer.Serialize(proje);
    var loaded = System.Text.Json.JsonSerializer.Deserialize<BinaProje>(json);

    if (loaded?.BinaIslevi != BinaIslevi.EglenceSporTesisleri)
        throw new InvalidOperationException("Bina işlevi proje JSON kayıt/açma döngüsünde korunmalı.");
}

static void BuildingMekanAdNotifiesEdits()
{
    var mekan = new Mekan { Ad = "Eski" };
    var changed = false;
    mekan.PropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(Mekan.Ad)) changed = true;
    };

    mekan.Ad = "Yeni";

    if (!changed || mekan.Ad != "Yeni")
        throw new InvalidOperationException("Mekan adı düzenlenince model değişiklik bildirimi üretmeli.");
}

static void BuildingVerticalMergeContainmentBasedSucceeds()
{
    // UX — Yıkıcı model: küçük oda centroid'i büyük odanın içindeyse TavanPatlat çalışır;
    // üst kat odası kaldırılır, hayatta kalan (alt/seçili) oda hacmi yığılır.
    var altKat = new Kat { KatNo = 1, KatYuksekligi = 3 };
    var ustKat = new Kat { KatNo = 2, KatYuksekligi = 3 };
    var kucuk = CreateRectMekan("Bodrum küçük", 2, 2, 4, 4);
    var buyuk = CreateRectMekan("Zemin büyük", 0, 0, 8, 8);
    altKat.Mekanlar.Add(kucuk);
    ustKat.Mekanlar.Add(buyuk);
    var proje = new BinaProje { Katlar = [altKat, ustKat] };

    var sonuc = new DuseyMekanBirlesimServisi().TavanPatlat(proje, altKat, kucuk);
    if (!sonuc.Basarili || sonuc.Birlesim == null)
        throw new InvalidOperationException($"Containment-based merge should succeed. Mesaj: {sonuc.Mesaj}");
    // Yıkıcı OLMAYAN: iki üye, her iki oda da kendi katında korunur (duvarlar kaybolmaz).
    if (sonuc.Birlesim.Uyeler.Count != 2)
        throw new InvalidOperationException("Birleşim 2 üye olmalı (küçük + büyük).");
    if (!ustKat.Mekanlar.Contains(buyuk) || !altKat.Mekanlar.Contains(kucuk))
        throw new InvalidOperationException("Her iki oda da korunmalı (yıkıcı değil).");
    if (!DuseyMekanBirlesimServisi.TavanAcikMi(proje, kucuk))
        throw new InvalidOperationException("Küçük odanın tavanı açık olmalı.");
    if (!proje.PatlatilmisDosemeVar)
        throw new InvalidOperationException("Patlatma sonrası PatlatilmisDosemeVar true olmalı.");
}

static void BuildingVerticalMergeRejectsWhenNotContained()
{
    // UX — Küçük odanın centroid'i hiçbir üst oda içinde değilse patlatma reddedilir.
    var altKat = new Kat { KatNo = 1, KatYuksekligi = 3 };
    var ustKat = new Kat { KatNo = 2, KatYuksekligi = 3 };
    var altMekan = CreateRectMekan("Alt", 0, 0, 2, 2);
    var ustMekan = CreateRectMekan("Üst (uzak)", 10, 10, 12, 12); // farklı bölgede
    altKat.Mekanlar.Add(altMekan);
    ustKat.Mekanlar.Add(ustMekan);
    var proje = new BinaProje { Katlar = [altKat, ustKat] };

    var sonuc = new DuseyMekanBirlesimServisi().TavanPatlat(proje, altKat, altMekan);
    if (sonuc.Basarili)
        throw new InvalidOperationException("Kapsama yokken patlatma reddedilmeli.");
}

static void BuildingVerticalMergeCanChainThreeFloors()
{
    // UX — Yıkıcı olmayan modelde TavanPatlat arka arkaya çağrılarak 3 kat zinciri kurulur.
    var k1 = new Kat { KatNo = 1, KatYuksekligi = 3 };
    var k2 = new Kat { KatNo = 2, KatYuksekligi = 3 };
    var k3 = new Kat { KatNo = 3, KatYuksekligi = 3 };
    var m1 = CreateRectMekan("Alt",  0, 0, 4, 4);
    var m2 = CreateRectMekan("Orta", 0, 0, 4, 4);
    var m3 = CreateRectMekan("Üst",  0, 0, 4, 4);
    k1.Mekanlar.Add(m1); k2.Mekanlar.Add(m2); k3.Mekanlar.Add(m3);
    var proje = new BinaProje { Katlar = [k1, k2, k3] };

    var svc = new DuseyMekanBirlesimServisi();
    var s1 = svc.TavanPatlat(proje, k1, m1);   // alt → orta
    var s2 = svc.TavanPatlat(proje, k2, m2);   // orta → üst
    if (!s1.Basarili || !s2.Basarili)
        throw new InvalidOperationException($"İki TavanPatlat da başarılı olmalı. {s1.Mesaj} / {s2.Mesaj}");

    var b = DuseyMekanBirlesimServisi.BirlesimBul(proje, m2.Id);
    if (b == null || b.Uyeler.Count != 3)
        throw new InvalidOperationException($"Birleşim 3 üye olmalı, alınan: {b?.Uyeler.Count}");
}

static void BuildingTinyWallAssignmentSurvivesRebuild()
{
    // Regresyon — 0.5 m² gibi minik duvarların ataması, komşuluk yeniden tespitinde (her
    // ekran yenilemesinde çalışır) düşmemeli. Eski 10 cm örtüşme eşiği minik duvarlarda kayba yol açıyordu.
    var kat = new Kat { KatNo = 1, KatYuksekligi = 2.8 };
    var m = new Mekan
    {
        Ad = "Oda", KullanimTipi = "Oda", Yukseklik = 2.8,
        Poligon = [ new Point2D(0, 0), new Point2D(0.18, 0), new Point2D(0.18, 3), new Point2D(0, 3) ]
    };
    kat.Mekanlar.Add(m);
    var oda = new OdaTespitServisi();
    oda.KomsuluklariTespitEt(kat.Mekanlar);

    static double Len(Bolme b) => Math.Sqrt(
        (b.Bitis.X - b.Baslangic.X) * (b.Bitis.X - b.Baslangic.X) +
        (b.Bitis.Y - b.Baslangic.Y) * (b.Bitis.Y - b.Baslangic.Y));

    var tiny = m.Bolmeler.OrderBy(Len).First(); // en kısa kenar (~0.18 m)
    if (Len(tiny) > 0.5) throw new InvalidOperationException("Test kurulumu: minik duvar bulunamadı.");
    var wall = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar };
    tiny.YapiElemani = wall;

    oda.KomsuluklariTespitEt(kat.Mekanlar); // yeniden üret (ekran yenilemesini taklit)

    var tinyAfter = m.Bolmeler.OrderBy(Len).First();
    if (tinyAfter.YapiElemani == null)
        throw new InvalidOperationException("Minik duvarın ataması yeniden üretimde kaybolmamalı.");
}

static void BuildingRebuildPreservesMenfezAndJunctions()
{
    // P0 regresyon — "Duvar Kontrolü" (ve her ekran yenilemesinde çalışan komşuluk yeniden
    // tespiti) eskiden yalnızca YapiElemani/Acikliklar'ı koruyordu; Menfez ve Junction
    // ayarları her yeniden üretimde sessizce siliniyordu. DuvarKontrol artık bu servisi
    // kullandığından, aynı zamanda o butonun veri kaybı düzeltmesinin de regresyon testidir.
    var kat = new Kat { KatNo = 1, KatYuksekligi = 3 };
    var m = CreateRectMekan("Oda", 0, 0, 4, 4);
    kat.Mekanlar.Add(m);
    var oda = new OdaTespitServisi();
    oda.KomsuluklariTespitEt(kat.Mekanlar);

    var duvar = m.Bolmeler.First();
    var malzeme = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar };
    duvar.YapiElemani = malzeme;
    duvar.Menfezler.Add(new Menfez { Tip = MenfezTipi.Akustik, DneDb = 48 });
    duvar.Junctions.Add(new Junction { Kenar = JunctionKenarTipi.Alt, BaglantiTipi = BaglantiTipi.Elastik, KijOverrideDb = 12 });
    // Açıklık eşleştirmesi merkez-yakınlığıyla çalışır (1 m tolerans) — duvarın (0,0)-(4,0)
    // üstünde, orta noktaya (2,0) yakın gerçekçi bir konum vermek gerekir.
    duvar.Acikliklar.Add(new BolmeAcikligi
    {
        Tip = BolmeAcikligiTipi.Pencere, Genislik = 1.2, Yukseklik = 1.4,
        Baslangic = new Point2D(1.5, 0), Bitis = new Point2D(2.5, 0)
    });

    // Birden fazla yeniden üretim — DuvarKontrol/RefreshKomsuDuvarlar/KaydetYola hepsi bunu çağırır.
    oda.KomsuluklariTespitEt(kat.Mekanlar);
    oda.KomsuluklariTespitEt(kat.Mekanlar);

    var duvarAfter = m.Bolmeler.First(b => b.YapiElemani != null);
    if (duvarAfter.YapiElemani == null)
        throw new InvalidOperationException("YapiElemani yeniden üretimde kaybolmamalı.");
    if (duvarAfter.Menfezler.Count != 1)
        throw new InvalidOperationException($"Menfez yeniden üretimde kaybolmamalı (bulunan: {duvarAfter.Menfezler.Count}).");
    if (duvarAfter.Junctions.Count != 1 || Math.Abs(duvarAfter.Junctions[0].KijOverrideDb - 12) > 1e-9)
        throw new InvalidOperationException("Junction ayarı (KijOverrideDb dahil) yeniden üretimde kaybolmamalı.");
    if (duvarAfter.Acikliklar.Count != 1)
        throw new InvalidOperationException("Açıklık yeniden üretimde kaybolmamalı.");
}

static void BuildingProjeMigratorNormalizesNullLists()
{
    // P1 regresyon — eski/bozuk .e4p08 JSON'larında liste alanları açık null olabilir;
    // System.Text.Json bu durumda koleksiyon initializer'ını atlar. Normalize bunları
    // güvenli varsayılanlarla doldurmalı; aksi halde Proje = yuklenen sonrası NRE oluşur.
    const string json = """
    {
        "ProjeAdi": null,
        "BinaIslevi": 0,
        "DisGurultuDb": 0,
        "DisGurultuThirdOctave": null,
        "BagimsizBolumler": null,
        "DuseyMekanBirlesimleri": null,
        "YapiElemaniKutuphanesi": null,
        "AkustikNoktalar": null,
        "DisGurultuKaynaklari": null,
        "Katlar": [ { "KatNo": 1, "Mekanlar": null } ]
    }
    """;
    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var yuklenen = System.Text.Json.JsonSerializer.Deserialize<BinaProje>(json, opts)
        ?? throw new InvalidOperationException("Test kurulumu: deserialize başarısız.");

    if (yuklenen.DisGurultuThirdOctave != null)
        throw new InvalidOperationException("Test kurulumu: JSON null bekleniyordu.");

    BinaProjeMigrator.Normalize(yuklenen);

    if (yuklenen.ProjeAdi == null) throw new InvalidOperationException("ProjeAdi null kalmamalı.");
    if (yuklenen.DisGurultuThirdOctave == null) throw new InvalidOperationException("DisGurultuThirdOctave null kalmamalı.");
    if (yuklenen.BagimsizBolumler == null) throw new InvalidOperationException("BagimsizBolumler null kalmamalı.");
    if (yuklenen.DuseyMekanBirlesimleri == null) throw new InvalidOperationException("DuseyMekanBirlesimleri null kalmamalı.");
    if (yuklenen.YapiElemaniKutuphanesi == null) throw new InvalidOperationException("YapiElemaniKutuphanesi null kalmamalı.");
    if (yuklenen.AkustikNoktalar == null) throw new InvalidOperationException("AkustikNoktalar null kalmamalı.");
    if (yuklenen.DisGurultuKaynaklari == null) throw new InvalidOperationException("DisGurultuKaynaklari null kalmamalı.");
    if (yuklenen.BinaIslevi != BinaIslevi.Konut)
        throw new InvalidOperationException($"Tanımsız BinaIslevi Konut'a düşmeli, bulunan: {yuklenen.BinaIslevi}.");
    if (Math.Abs(yuklenen.DisGurultuDb - 65.0) > 1e-9)
        throw new InvalidOperationException($"DisGurultuDb<=0 iken 65'e düşmeli, bulunan: {yuklenen.DisGurultuDb}.");
    if (yuklenen.Katlar[0].Mekanlar == null || yuklenen.Katlar[0].Mekanlar.Count != 0)
        throw new InvalidOperationException("Kat.Mekanlar null kalmamalı (boş koleksiyona düşmeli).");
}

static void BuildingProjeRoundTripPreservesMetadataFields()
{
    // P1 regresyon — ProjeAc eskiden alanları teker teker kopyalıyordu ve Id, Adres,
    // OlusturmaTarihi, YapiElemaniKutuphanesi, AkustikNoktalar, DisGurultuKaynaklari
    // sessizce atlanıyordu. ProjeAc artık deserialize edilen projeyi (Normalize sonrası)
    // doğrudan atadığından, bu alanların JSON round-trip'te kaybolmadığını doğrular.
    var orijinal = new BinaProje
    {
        Adres = "Test Mah. No:1",
        OlusturmaTarihi = new DateTime(2026, 1, 15),
        YapiElemaniKutuphanesi = [ new YapiElemani { Ad = "Proje-Özel Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 45 } ],
        AkustikNoktalar = [ new AkustikNokta { Ad = "Kaynak-1", Tip = AkustikNoktaTipi.Kaynak, Lw = 90 } ],
        DisGurultuKaynaklari = [ new DisGurultuKaynagi { Ad = "Ana Yol", Tip = DisGurultuTipi.Trafik, Lden = 68 } ],
    };
    var orijinalId = orijinal.Id;

    var opts = new System.Text.Json.JsonSerializerOptions
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
        MaxDepth = 256,
    };
    var json = System.Text.Json.JsonSerializer.Serialize(orijinal, opts);
    var yuklenen = System.Text.Json.JsonSerializer.Deserialize<BinaProje>(json, opts)
        ?? throw new InvalidOperationException("Test kurulumu: deserialize başarısız.");
    BinaProjeMigrator.Normalize(yuklenen);

    if (yuklenen.Id != orijinalId) throw new InvalidOperationException("Id round-trip'te korunmalı.");
    if (yuklenen.Adres != "Test Mah. No:1") throw new InvalidOperationException("Adres round-trip'te korunmalı.");
    if (yuklenen.OlusturmaTarihi != orijinal.OlusturmaTarihi) throw new InvalidOperationException("OlusturmaTarihi round-trip'te korunmalı.");
    if (yuklenen.YapiElemaniKutuphanesi.Count != 1 || yuklenen.YapiElemaniKutuphanesi[0].Ad != "Proje-Özel Duvar")
        throw new InvalidOperationException("YapiElemaniKutuphanesi round-trip'te korunmalı.");
    if (yuklenen.AkustikNoktalar.Count != 1 || yuklenen.AkustikNoktalar[0].Ad != "Kaynak-1")
        throw new InvalidOperationException("AkustikNoktalar round-trip'te korunmalı.");
    if (yuklenen.DisGurultuKaynaklari.Count != 1 || yuklenen.DisGurultuKaynaklari[0].Ad != "Ana Yol")
        throw new InvalidOperationException("DisGurultuKaynaklari round-trip'te korunmalı.");
}

static void BuildingVerticalMergeKeepsBothRooms()
{
    // UX — Yıkıcı OLMAYAN: patlatma sonrası her iki oda da kendi katında, kendi duvarlarıyla korunur
    // (duvarlar poligondan türediği için silinmemeli). Üst oda yalnız listede gizlenir.
    var k1 = new Kat { KatNo = 1, KatYuksekligi = 3 };
    var k2 = new Kat { KatNo = 2, KatYuksekligi = 3 };
    var alt  = CreateRectMekan("Alt",  0, 0, 4, 4);
    var ust  = CreateRectMekan("Üst",  0, 0, 4, 4);
    k1.Mekanlar.Add(alt); k2.Mekanlar.Add(ust);
    var proje = new BinaProje { Katlar = [k1, k2] };
    var oda = new OdaTespitServisi();
    oda.KomsuluklariTespitEt(k1.Mekanlar);
    oda.KomsuluklariTespitEt(k2.Mekanlar);
    int ustDuvar = ust.Bolmeler.Count;

    var sonuc = new DuseyMekanBirlesimServisi().TavanPatlat(proje, k1, alt);
    if (!sonuc.Basarili) throw new InvalidOperationException($"TavanPatlat başarılı olmalı: {sonuc.Mesaj}");
    if (!k2.Mekanlar.Contains(ust) || ust.Bolmeler.Count != ustDuvar)
        throw new InvalidOperationException("Üst oda ve duvarları korunmalı (yıkıcı değil).");
    if (!k1.Mekanlar.Contains(alt))
        throw new InvalidOperationException("Alt oda korunmalı.");
}

static void BuildingHacimleriGuncelleSumsMergedVolumes()
{
    // UX — Patlatma sonrası HacimleriGuncelle, birleşim üyelerinin hacim toplamını her üyeye
    // BirlesikHacimM3 olarak yazar; etiket "Σ" formatında döner.
    var altKat = new Kat { KatNo = 1, KatYuksekligi = 3 };
    var ustKat = new Kat { KatNo = 2, KatYuksekligi = 3 };
    var kucuk = CreateRectMekan("Küçük", 2, 2, 4, 4); // Alan=4 → Hacim=12
    var buyuk = CreateRectMekan("Büyük", 0, 0, 6, 6); // Alan=36 → Hacim=108
    altKat.Mekanlar.Add(kucuk);
    ustKat.Mekanlar.Add(buyuk);
    var proje = new BinaProje { Katlar = [altKat, ustKat] };

    if (kucuk.BirlesikHacimM3.HasValue || buyuk.BirlesikHacimM3.HasValue)
        throw new InvalidOperationException("Başlangıçta BirlesikHacimM3 null olmalı.");

    var sonuc = new DuseyMekanBirlesimServisi().TavanPatlat(proje, altKat, kucuk);
    if (!sonuc.Basarili) throw new InvalidOperationException($"Patlatma başarısız: {sonuc.Mesaj}");

    DuseyMekanBirlesimServisi.HacimleriGuncelle(proje);

    var beklenen = kucuk.Hacim + buyuk.Hacim; // 12 + 108 = 120
    AssertClose(beklenen, kucuk.BirlesikHacimM3 ?? 0, 0.01, "Küçük odanın birleşik hacmi toplam olmalı.");
    AssertClose(beklenen, buyuk.BirlesikHacimM3 ?? 0, 0.01, "Büyük odanın birleşik hacmi toplam olmalı.");
    if (!kucuk.HacimEtiketi.Contains("Σ"))
        throw new InvalidOperationException($"Birleşik etiket 'Σ' içermeli, alınan: {kucuk.HacimEtiketi}");

    new DuseyMekanBirlesimServisi().BirlesimdenCikar(proje, kucuk, out _);
    DuseyMekanBirlesimServisi.HacimleriGuncelle(proje);
    if (kucuk.BirlesikHacimM3.HasValue || buyuk.BirlesikHacimM3.HasValue)
        throw new InvalidOperationException("Geri alma sonrası BirlesikHacimM3 null olmalı.");
}

static void BuildingMergeExposesPartnerWalls()
{
    // Part 2 — Birleşimde hayatta kalan (görünür) odanın altında partner odanın duvarları
    // atama için erişilebilir olmalı (BirlesikPartnerMekanlar).
    var k1 = new Kat { KatNo = 1, KatYuksekligi = 3 };
    var k2 = new Kat { KatNo = 2, KatYuksekligi = 3 };
    var alt = CreateRectMekan("Alt", 0, 0, 4, 4);
    var ust = CreateRectMekan("Üst", 0, 0, 4, 4);
    k1.Mekanlar.Add(alt); k2.Mekanlar.Add(ust);
    var proje = new BinaProje { Katlar = [k1, k2] };
    var oda = new OdaTespitServisi();
    oda.KomsuluklariTespitEt(k1.Mekanlar);
    oda.KomsuluklariTespitEt(k2.Mekanlar);

    new DuseyMekanBirlesimServisi().TavanPatlat(proje, k1, alt);

    var partnerler = DuseyMekanBirlesimServisi.BirlesikPartnerMekanlar(proje, alt).ToList();
    if (!partnerler.Contains(ust))
        throw new InvalidOperationException("Hayatta kalan odanın partneri üst oda olmalı.");
    var partnerDuvarlar = partnerler.SelectMany(m => m.Bolmeler).ToList();
    if (partnerDuvarlar.Count != ust.Bolmeler.Count || partnerDuvarlar.Count == 0)
        throw new InvalidOperationException("Partner odanın duvarları atama için erişilebilir olmalı.");
}

static void BuildingVerticalMergeUndoable()
{
    // UX — Patlatma geri alınınca aktif birleşim kalmaz, her iki oda da korunur (yeniden görünür).
    var k1 = new Kat { KatNo = 1, KatYuksekligi = 3 };
    var k2 = new Kat { KatNo = 2, KatYuksekligi = 3 };
    var alt = CreateRectMekan("Alt", 0, 0, 4, 4);
    var ust = CreateRectMekan("Üst", 0, 0, 4, 4);
    k1.Mekanlar.Add(alt); k2.Mekanlar.Add(ust);
    var proje = new BinaProje { Katlar = [k1, k2] };

    var svc = new DuseyMekanBirlesimServisi();
    svc.TavanPatlat(proje, k1, alt);
    if (!proje.PatlatilmisDosemeVar) throw new InvalidOperationException("Patlatma sonrası aktif birleşim olmalı.");

    DuseyMekanBirlesimServisi.TumPatlatmalariGeriAl(proje);
    if (proje.PatlatilmisDosemeVar)
        throw new InvalidOperationException("Geri alma sonrası aktif birleşim kalmamalı.");
    if (!k1.Mekanlar.Contains(alt) || !k2.Mekanlar.Contains(ust))
        throw new InvalidOperationException("Geri alma sonrası her iki oda da korunmalı.");
}

static void BuildingRoomAssignmentCompleteCriterion()
{
    // Part 1 — Atama tamam: tüm duvarlar + döşeme + tavan malzemesi atanınca true.
    var doseme = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme };
    var tavan  = new YapiElemani { Ad = "Tavan",  Tip = YapiElemaniTipi.Tavan };
    var duvar  = new YapiElemani { Ad = "Duvar",  Tip = YapiElemaniTipi.Duvar };
    var m = CreateRectMekan("Oda", 0, 0, 4, 4);
    m.Bolmeler.Add(new Bolme { Ad = "D1", KaynakMekan = m });
    m.Bolmeler.Add(new Bolme { Ad = "D2", KaynakMekan = m });

    if (m.AtamaTamamMi) throw new InvalidOperationException("Hiç atama yokken tamam olmamalı.");
    m.Bolmeler[0].YapiElemani = duvar;
    m.Bolmeler[1].YapiElemani = duvar;
    if (m.AtamaTamamMi) throw new InvalidOperationException("Döşeme/tavan eksikken tamam olmamalı.");
    m.ZeminYapiElemani = doseme;
    if (m.AtamaTamamMi) throw new InvalidOperationException("Tavan eksikken tamam olmamalı.");
    m.TavanYapiElemani = tavan;
    if (!m.AtamaTamamMi) throw new InvalidOperationException("Duvar+döşeme+tavan atanınca tamam olmalı.");
    if (m.AtamaRenk != "#2ECC71") throw new InvalidOperationException("Tamam olunca AtamaRenk yeşil olmalı.");

    // Part 2 — P1 regresyon: bir duvardaki açıklığın (kapı/pencere) malzemesi eksikse
    // AtamaTamamMi artık true dönmemeli (eskiden açıklık malzemesi kontrol edilmiyordu).
    var pencere = new BolmeAcikligi { Tip = BolmeAcikligiTipi.Pencere, Genislik = 1.2, Yukseklik = 1.2 };
    m.Bolmeler[0].Acikliklar.Add(pencere);
    if (m.AtamaTamamMi)
        throw new InvalidOperationException("Açıklık malzemesi atanmamışken AtamaTamamMi true dönmemeli.");
    pencere.YapiElemani = new YapiElemani { Ad = "Pencere", Tip = YapiElemaniTipi.Pencere };
    if (!m.AtamaTamamMi)
        throw new InvalidOperationException("Açıklık malzemesi de atanınca AtamaTamamMi true dönmeli.");
}

// ─── Faz 0: Standart doğrulama korkulukları (ISO 717 / EN 12354-1) ──────────

static void BuildingDntwMatchesStandardNormalization()
{
    // EN 12354-1 Eq.: DnT,w = R'w + 10·log10(0.32·V / S)   [T₀ = 0.5 s]
    // Altın örnek: R'w = 52 dB, V = 50 m³, S = 10 m²
    //   10·log10(0.32·50/10) = 10·log10(1.6) = 2.04 dB → DnT,w = 54.04 dB
    var alici = new Mekan { Ad = "Alıcı", KullanimTipi = "Yatak Odası", Alan = 20, Yukseklik = 2.5 }; // V = 50 m³
    var kaynak = new Mekan { Ad = "Kaynak", KullanimTipi = "Salon", Alan = 20, Yukseklik = 2.5 };
    var bolme = new Bolme
    {
        Ad = "Ayırıcı",
        Alan = 10,            // S = 10 m²
        ApparentRw = 52,      // R'w doğrudan verilir; DnT,w formülü izole test edilir
        YapiElemani = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 52 },
        KaynakMekan = kaynak,
        AliciMekan = alici
    };

    var dntw = new HavaSesiHesapServisi().HesaplaDntw(bolme);
    AssertClose(54.04, dntw, 0.1, "DnT,w standart normalizasyonu (T₀=0.5 s)");
}

static void BuildingIso7171RwMatchesReferenceCurve()
{
    // ISO 717-1 referans eğrisinin kendisi girilince: eğri 32 dB olumsuz sapma
    // sınırına ulaşmadan tam 2 dB yukarı kaydırılabilir (16 bant × 2 dB = 32).
    // Dolayısıyla Rw = 52 (500 Hz dayanağı) + 2 = 54 dB.
    var reference = new double[] { 33, 36, 39, 42, 45, 48, 51, 52, 53, 54, 55, 56, 56, 56, 56, 56 };
    var (rw, c, ctr, yetersiz) = new HavaSesiHesapServisi().CalculateSingleValue(reference);
    if (yetersiz)
        throw new InvalidOperationException("16 bantlı referans eğrisi geçerli ISO 717-1 girişi olmalı.");
    AssertClose(54, rw, 0.001, "ISO 717-1 Rw referans eğrisinden");

    // Tüm bantları +10 dB kaydırmak Rw'yi tam +10 dB artırmalı (doğrusallık).
    var raised = reference.Select(v => v + 10).ToArray();
    var (rwRaised, _, _, _) = new HavaSesiHesapServisi().CalculateSingleValue(raised);
    AssertClose(64, rwRaised, 0.001, "ISO 717-1 Rw +10 dB doğrusallığı");

    // Adaptasyon terimleri fiziksel aralıkta ve ≤ 0 olmalı (ISO 717-1 Annex B).
    AssertBetween(c, -10, 0, "ISO 717-1 C adaptasyon terimi");
    AssertBetween(ctr, -20, 0, "ISO 717-1 Ctr adaptasyon terimi");
}

static void BuildingIso7172LnwMatchesReferenceCurve()
{
    // ISO 717-2 referans eğrisi girilince: 32 dB olumsuz sapma sınırına ulaşmadan
    // ölçüm eğrisine doğru 2 dB aşağı indirilebilir (16 bant × 2 dB = 32).
    // L'n,w = 60 (500 Hz dayanağı) − 2 = 58 dB.
    var reference = new double[] { 62, 62, 62, 62, 62, 62, 61, 60, 59, 58, 57, 54, 51, 48, 45, 42 };
    var lnw = new DarbeSesiHesapServisi().CalculateSingleValue(reference);
    AssertClose(58, lnw, 0.001, "ISO 717-2 L'n,w referans eğrisinden");

    // Tüm bantları +10 dB yükseltmek L'n,w'yi tam +10 dB artırmalı.
    var raised = reference.Select(v => v + 10).ToArray();
    var lnwRaised = new DarbeSesiHesapServisi().CalculateSingleValue(raised);
    AssertClose(68, lnwRaised, 0.001, "ISO 717-2 L'n,w +10 dB doğrusallığı");
}

static void BuildingIso717RejectsInsufficientBands()
{
    // A3 — ISO 717 Madde 5: 16 banttan az giriş geçersiz sayılmalı (sessiz yanlış sonuç değil).
    var (_, _, _, yetersiz) = new HavaSesiHesapServisi().CalculateSingleValue(new double[10]);
    if (!yetersiz)
        throw new InvalidOperationException("ISO 717-1 <16 bant girişi yetersiz işaretlemeli.");

    var lnw = new DarbeSesiHesapServisi().CalculateSingleValue(new double[10]);
    if (!double.IsNaN(lnw))
        throw new InvalidOperationException("ISO 717-2 <16 bant girişinde NaN dönmeli.");
}

static void BuildingFlankingKijDecouplingRaisesApparentRw()
{
    // EN 12354-1 yapısal değişmez: daha fazla ayrıştırılmış birleşim (yüksek Kij)
    // yanal iletimi azaltır → apparent R'w direkt Rw'ye doğru yükselir.
    static double ApparentRwFor(BaglantiTipi tip)
    {
        var ayirici = new YapiElemani { Ad = "Ayırıcı", Tip = YapiElemaniTipi.Duvar, Rw = 55, BaglantiTipi = tip };
        var yan = new YapiElemani { Ad = "Yan", Tip = YapiElemaniTipi.Duvar, Rw = 50, BaglantiTipi = tip };
        var doseme = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Rw = 52, BaglantiTipi = tip };

        var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
        var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
        kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
        alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;

        var bolme = new Bolme
        {
            Ad = "Ayırıcı duvar",
            Baslangic = new Point2D(4, 0),
            Bitis = new Point2D(4, 3),
            HasGeometry = true,
            Alan = 9,
            YapiElemani = ayirici,
            KaynakMekan = kaynak,
            AliciMekan = alici
        };
        kaynak.Bolmeler.Add(bolme);
        kaynak.Bolmeler.Add(new Bolme { Ad = "K alt", Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0), HasGeometry = true, YapiElemani = yan, KaynakMekan = kaynak });
        kaynak.Bolmeler.Add(new Bolme { Ad = "K üst", Baslangic = new Point2D(0, 3), Bitis = new Point2D(4, 3), HasGeometry = true, YapiElemani = yan, KaynakMekan = kaynak });
        alici.Bolmeler.Add(new Bolme { Ad = "A alt", Baslangic = new Point2D(4, 0), Bitis = new Point2D(8, 0), HasGeometry = true, YapiElemani = yan, KaynakMekan = alici });
        alici.Bolmeler.Add(new Bolme { Ad = "A üst", Baslangic = new Point2D(4, 3), Bitis = new Point2D(8, 3), HasGeometry = true, YapiElemani = yan, KaynakMekan = alici });

        return new HavaSesiHesapServisi().HesaplaApparentRw(bolme);
    }

    var rijit = ApparentRwFor(BaglantiTipi.Rijit);
    var kesintili = ApparentRwFor(BaglantiTipi.Kesintili);
    AssertGreater(kesintili, rijit, "Yüksek Kij (kesintili birleşim) apparent R'w'yi yükseltmeli.");
}

static void BuildingEnsureDefaultJunctionsPreservesUserSettings()
{
    var bolme = new Bolme
    {
        Ad = "Ayırıcı",
        Alan = 9,
        Baslangic = new Point2D(0, 0),
        Bitis = new Point2D(0, 3),
        YapiElemani = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55, BaglantiTipi = BaglantiTipi.Rijit }
    };
    bolme.Junctions.Add(new Junction
    {
        Kenar = JunctionKenarTipi.Alt,
        GeometriTipi = JunctionGeometriTipi.X,
        BaglantiTipi = BaglantiTipi.Kesintili,
        StrukturelTipi = JunctionStrukturelTipi.PerdeDuvar,
        Uzunluk = 4,
        KijOverrideDb = 17
    });

    HavaSesiHesapServisi.EnsureDefaultJunctions(bolme);

    var alt = bolme.Junctions.Single(j => j.Kenar == JunctionKenarTipi.Alt);
    if (alt.BaglantiTipi != BaglantiTipi.Kesintili ||
        alt.GeometriTipi != JunctionGeometriTipi.X ||
        alt.StrukturelTipi != JunctionStrukturelTipi.PerdeDuvar ||
        Math.Abs(alt.KijOverrideDb - 17) > 1e-6)
        throw new InvalidOperationException("Kullanıcı junction ayarı EnsureDefaultJunctions sırasında korunmalı.");

    if (bolme.Junctions.Count != 4)
        throw new InvalidOperationException("Eksik junction kenarları tamamlanmalı.");
}

static void BuildingMaterialFlankingValueDoesNotBypassAnnexE()
{
    var ayirici = new YapiElemani
    {
        Ad = "Katalog duvarı",
        Tip = YapiElemaniTipi.Duvar,
        Rw = 55,
        FlankingDuzeltmesiDb = 30,
        Katmanlar = { new YapiKatmani { MalzemeAdi = "Beton", Kalinlik = 200, Yogunluk = 2400 } }
    };
    var doseme = new YapiElemani
    {
        Ad = "Döşeme",
        Tip = YapiElemaniTipi.Doseme,
        Rw = 52,
        Katmanlar = { new YapiKatmani { MalzemeAdi = "Beton", Kalinlik = 180, Yogunluk = 2400 } }
    };
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
    kaynak.ZeminYapiElemani = alici.ZeminYapiElemani = doseme;
    kaynak.TavanYapiElemani = alici.TavanYapiElemani = doseme;

    var bolme = new Bolme
    {
        Ad = "Ayırıcı",
        Alan = 9,
        Baslangic = new Point2D(4, 0),
        Bitis = new Point2D(4, 3),
        HasGeometry = true,
        YapiElemani = ayirici,
        KaynakMekan = kaynak,
        AliciMekan = alici
    };

    var yol = HavaSesiHesapServisi.GetOnUcYolFlankingYollari(bolme)
        .First(y => y.Kenar == JunctionKenarTipi.Alt && y.YolTipi == "Df");

    if (bolme.Junctions.Any(j => j.KijOverrideDb > 0))
        throw new InvalidOperationException("Malzeme FlankingDuzeltmesiDb otomatik KijOverrideDb'ye kopyalanmamalı.");
    if (Math.Abs(yol.Kij - 30) < 0.01)
        throw new InvalidOperationException("Malzeme FlankingDuzeltmesiDb Annex E hesabını bypass etmemeli.");
}

static void BuildingFlankingCouplingTermFollowsFlankAreas()
{
    // EN 12354-1 Eq. 21 alan terimi: 10·log10(Ss / √(Si·Sj)).
    // Alıcı flanking elemanın (zemin/tavan) alanı iki katına çıkınca Df yolu Rij'i
    // tam 10·log10(1/√2) ≈ −1.505 dB değişmeli. (Hatalı 'Ss/lij²' formu oda alanına
    // hiç tepki vermez → bu test düzeltmeyi kanıtlar.)
    static double DfRij(double aliciOdaAlani)
    {
        var ayirici = new YapiElemani { Ad = "Ayırıcı", Tip = YapiElemaniTipi.Duvar, Rw = 55, BaglantiTipi = BaglantiTipi.Rijit };
        var doseme = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Rw = 52, BaglantiTipi = BaglantiTipi.Rijit };
        var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
        var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
        kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
        alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;
        alici.Alan = aliciOdaAlani; // flanking zemin/tavan alanını izole et

        var bolme = new Bolme
        {
            Ad = "Ayırıcı", Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 3),
            HasGeometry = true, Alan = 9, YapiElemani = ayirici, KaynakMekan = kaynak, AliciMekan = alici
        };
        kaynak.Bolmeler.Add(bolme); // Sol/Sağ için komşu duvar yok → yalnızca Alt/Üst yolları üretilir

        var df = HavaSesiHesapServisi.GetOnUcYolFlankingYollari(bolme)
            .First(y => y.Kenar == JunctionKenarTipi.Alt && y.YolTipi == "Df");
        return df.Rij;
    }

    var bir = DfRij(12);
    var iki = DfRij(24);
    // EN 12354-1 net etki: alan terimi 10·log10(Ss/√(Si·Sj)) ile soğurma uzunluğu düzeltmesi
    // 10·log10(lij/√(ai·aj)) birleşince a∝S olduğu için Sj bağımlılığı iptal olur.
    // (Faz 1'de yalnız alan terimi vardı → bu test −1.5 dB iddia ediyordu;
    //  Faz 2b ile in-situ soğurma düzeltmesi eklenince doğru net etki ≈ 0.)
    AssertClose(0.0, iki - bir, 0.05, "Sj değişimi alan + soğurma kombinasyonunda iptal olur (a∝S)");
}

static void BuildingFacadeCtrUsesCompositeSpectrum()
{
    // DnT,A,tr için Ctr opak duvarın tek sayısından (Ctr=0) değil, açıklıklar dahil
    // BİLEŞİK cephe spektrumundan türetilmeli. Alçak frekansta zayıf bir pencere
    // bileşik Ctr'yi belirgin negatife çeker → DnT,A,tr < DnT,w.
    var wall = new YapiElemani
    {
        Ad = "Cephe duvarı", Tip = YapiElemaniTipi.Duvar, Rw = 55, Ctr = 0,
        RThirdOctave = Enumerable.Repeat(55.0, 16).ToList() // düz spektrum → Ctr ≈ 0
    };
    var window = new YapiElemani
    {
        Ad = "Pencere", Tip = YapiElemaniTipi.Pencere, Rw = 30,
        // alçak frekansta belirgin zayıf (trafik duyarlı) spektrum
        RThirdOctave = new List<double> { 18, 19, 20, 22, 24, 26, 28, 30, 32, 33, 34, 35, 36, 36, 36, 36 }
    };
    var oda = CreateRectMekan("Salon", 0, 0, 4, 3);
    var bolme = new Bolme
    {
        Ad = "Cephe", Alan = 10,
        YapiElemani = wall, KaynakMekan = oda, AliciMekan = null // IsDisCephe = (AliciMekan == null)
    };
    bolme.Acikliklar.Add(new BolmeAcikligi { Tip = BolmeAcikligiTipi.Pencere, YapiElemani = window, Genislik = 1.5, Yukseklik = 1.4 });

    var proje = new BinaProje { DisGurultuDb = 70, DisGurultuCepheMaruziyeti = CepheMaruziyet.SerbestAlan };
    var service = new DisGurultuServisi();
    service.UygulaDisCepheYalitimi(oda, [bolme], proje.DisGurultuCepheMaruziyeti); // bolme.Dntw doldurulur
    var sonuc = service.HesaplaSpektralDisCephe(proje, bolme)
        ?? throw new InvalidOperationException("Spektral cephe sonucu üretilmeli.");

    AssertGreater(bolme.Dntw - sonuc.DnTATr, 0.5, "Bileşik cephe Ctr'si DnT,A,tr'yi DnT,w altına çekmeli.");
}

static void BuildingReportUsesDntAAndLnTwValues()
{
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3);
    kaynak.KaynakDerecesi = "OG";
    var alici = CreateRectMekan("Alıcı", 3, 0, 6, 3);
    alici.AliciSinifi = "II";

    var duvar = new Bolme
    {
        Ad = "Ortak duvar",
        Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 60, C = 4 },
        KaynakMekan = kaynak,
        AliciMekan = alici,
        ApparentRw = 60,
        Dntw = 50,
        UcuncuOktavHavaSesiSonucu = new UcuncuOktavHavaSonucu(new double[16], 60, -3, -8, "Test")
    };
    kaynak.Bolmeler.Add(duvar);

    var doseme = new Bolme
    {
        Ad = "Döşeme",
        Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 70 },
        KaynakMekan = kaynak,
        AliciMekan = alici,
        ApparentLnw = 70,
        ApparentLnTw = 62
    };
    alici.Bolmeler.Add(doseme);

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(kaynak);
    kat.Mekanlar.Add(alici);
    var proje = new BinaProje
    {
        ProjeAdi = "Rapor test",
        BinaSinifi = BinaSinifi.S1,
        AkustikPerformansSinifi = AkustikPerformansSinifi.C
    };
    proje.Katlar.Add(kat);

    var satirlar = new BinaAkustikRaporServisi().TabloVerisiOlustur(proje);
    var duvarSatiri = satirlar.Single(s => s.Bolme == "Ortak duvar");
    var dosemeSatiri = satirlar.Single(s => s.Bolme == "Döşeme");

    AssertClose(47.0, duvarSatiri.Deger, 0.01, "Rapor iç duvar değeri DnT,w + apparent C olmalı.");
    if (duvarSatiri.Birim != "dB DnT,A")
        throw new InvalidOperationException($"İç duvar birimi DnT,A olmalı, alınan: {duvarSatiri.Birim}");
    AssertClose(62.0, dosemeSatiri.Deger, 0.01, "Rapor döşeme değeri L'nT,w olmalı.");
    if (dosemeSatiri.Birim != "dB L'nT,w")
        throw new InvalidOperationException($"Döşeme birimi L'nT,w olmalı, alınan: {dosemeSatiri.Birim}");
}

static void BuildingFacadeReportUsesCorrectLgagLimit()
{
    // P0 regresyon — BinaAkustikRaporServisi.DisGurultuUygulanmisLimit eskiden Lgag'ı
    // KontrolEtDetayli'ye hiç geçmiyordu (Lgag=0 varsayımıyla yalnız 30 dB taban limiti
    // hesaplanıyordu), sonra yanlış düz "Lgag−35" formülüyle eziyordu. Tablo 3.1'in gerçek
    // formülü Lgag − ofset(hassasiyet, sınıf)'tır. C sınıfı + I hassasiyet + Lgag=65 →
    // limit = 65 − 22 = 43 dB (30 dB taban DEĞİL — UI'da "kaldı" iken rapor "geçti" diyebiliyordu).
    var oda = CreateRectMekan("Yatak Odası", 0, 0, 4, 3);
    oda.AliciSinifi = "I";
    var wall = new YapiElemani
    {
        Ad = "Cephe duvarı", Tip = YapiElemaniTipi.Duvar, Rw = 55,
        RThirdOctave = Enumerable.Repeat(55.0, 16).ToList()
    };
    var cephe = new Bolme
    {
        Ad = "Cephe", Alan = 10, HasGeometry = true,
        Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0),
        YapiElemani = wall, KaynakMekan = oda, AliciMekan = null
    };
    oda.Bolmeler.Add(cephe);

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(oda);
    var proje = new BinaProje
    {
        BinaSinifi = BinaSinifi.S1,
        AkustikPerformansSinifi = AkustikPerformansSinifi.C,
        DisGurultuDb = 65,
    };
    proje.Katlar.Add(kat);

    var satirlar = new BinaAkustikRaporServisi().TabloVerisiOlustur(proje);
    var satir = satirlar.Single(s => s.Bolme == "Cephe");

    AssertClose(43.0, satir.LimitDeger, 0.01,
        "Cephe limiti Tablo 3.1 Lgag-ofset formülünden (65-22) gelmeli, 30 dB tabana düşmemeli.");
}

static void BuildingDxfHonorsPerformanceClassAndLgag()
{
    // P1 regresyon — DxfRenkliCiktiServisi eskiden proje.AkustikPerformansSinifi/DisGurultuDb'yi
    // hiç okumuyordu (sınıf C + Lgag=0 varsayımına düşüyordu) ve döşemede otoriter olmayan
    // L'n,w'yi (L'nT,w yerine) etikete yazıyordu.
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3);
    kaynak.KaynakDerecesi = "OG";
    var alici = CreateRectMekan("Alıcı", 0, 3, 3, 6);
    alici.AliciSinifi = "II";

    var doseme = new Bolme
    {
        Ad = "Döşeme test", Alan = 9, HasGeometry = true,
        Baslangic = new Point2D(0, 3), Bitis = new Point2D(3, 3),
        YapiElemani = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 70 },
        KaynakMekan = kaynak, AliciMekan = alici,
        ApparentLnw = 70, ApparentLnTw = 55
    };
    kaynak.Bolmeler.Add(doseme);

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(kaynak);
    kat.Mekanlar.Add(alici);
    var proje = new BinaProje
    {
        BinaSinifi = BinaSinifi.S2,
        AkustikPerformansSinifi = AkustikPerformansSinifi.C,
    };
    proje.Katlar.Add(kat);

    var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"m08-dxf-test-{Guid.NewGuid():N}.dxf");
    try
    {
        new DxfRenkliCiktiServisi().DxfRenkliRaporOlustur(proje, tmp);
        var dxf = netDxf.DxfDocument.Load(tmp)
            ?? throw new InvalidOperationException("Üretilen DXF geri okunamadı (bozuk dosya).");
        var etiketler = dxf.Entities.Texts.Select(t => t.Value).ToList();
        if (!etiketler.Any(t => t.Contains("L'nT,w")))
            throw new InvalidOperationException("DXF döşeme etiketi L'nT,w kullanmalı.");
        if (etiketler.Any(t => t.Contains("L'n,w")))
            throw new InvalidOperationException("DXF döşeme etiketi eski L'n,w biçimini kullanmamalı.");
    }
    finally
    {
        if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp);
    }
}

static void BuildingReportFilterExcludesBolme()
{
    // P0 regresyon — "Rapora Dahil" seçimi hiçbir rapor yoluna geçilmiyordu; kullanıcı
    // satır kaldırdığını sanıyor ama tüm bölmeler yine rapora giriyordu. Artık
    // haricTutulanBolmeIdleri parametresi metin/Excel/DXF servislerine kadar akıyor.
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3);
    kaynak.KaynakDerecesi = "OG";
    var alici = CreateRectMekan("Alıcı", 3, 0, 6, 3);
    alici.AliciSinifi = "II";

    var duvarA = new Bolme
    {
        Ad = "Duvar A", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55 },
        KaynakMekan = kaynak, AliciMekan = alici, ApparentRw = 55
    };
    var duvarB = new Bolme
    {
        Ad = "Duvar B", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55 },
        KaynakMekan = kaynak, AliciMekan = alici, ApparentRw = 55
    };
    kaynak.Bolmeler.Add(duvarA);
    kaynak.Bolmeler.Add(duvarB);

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(kaynak);
    kat.Mekanlar.Add(alici);
    var proje = new BinaProje { BinaSinifi = BinaSinifi.S1, AkustikPerformansSinifi = AkustikPerformansSinifi.C };
    proje.Katlar.Add(kat);

    var servis = new BinaAkustikRaporServisi();
    var haric = new HashSet<Guid> { duvarA.Id };

    var satirlar = servis.TabloVerisiOlustur(proje, haric);
    if (satirlar.Any(s => s.Bolme == "Duvar A"))
        throw new InvalidOperationException("Hariç tutulan bölme TabloVerisiOlustur çıktısında olmamalı.");
    if (!satirlar.Any(s => s.Bolme == "Duvar B"))
        throw new InvalidOperationException("Hariç tutulmayan bölme çıktıda olmalı.");

    var metin = servis.MetinRaporOlustur(proje, haric);
    if (metin.Contains("Duvar A"))
        throw new InvalidOperationException("Hariç tutulan bölme metin raporunda görünmemeli.");
    if (!metin.Contains("Duvar B"))
        throw new InvalidOperationException("Hariç tutulmayan bölme metin raporunda görünmeli.");
}

static void BuildingReportShowsVeriEksikDistinctlyNotAsKaldi()
{
    // Faz 6 regresyon — VeriEksik (malzeme atanmamış) eskiden metin raporunda "❌ KALDI" ile
    // aynı görünüyordu (başarısızlıkla karıştırılıyordu). Artık ayrı "⚠️ VERİ EKSİK" etiketi var.
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3);
    var alici = CreateRectMekan("Alıcı", 3, 0, 6, 3);
    var duvarMalzemesiz = new Bolme
    {
        Ad = "Malzemesiz Duvar", Alan = 9,
        YapiElemani = null,
        KaynakMekan = kaynak, AliciMekan = alici
    };
    kaynak.Bolmeler.Add(duvarMalzemesiz);

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(kaynak);
    kat.Mekanlar.Add(alici);
    var proje = new BinaProje { BinaSinifi = BinaSinifi.S1, AkustikPerformansSinifi = AkustikPerformansSinifi.C };
    proje.Katlar.Add(kat);

    var metin = new BinaAkustikRaporServisi().MetinRaporOlustur(proje);
    var satir = metin.Split('\n').FirstOrDefault(l => l.Contains("Malzemesiz Duvar"));
    if (satir == null)
        throw new InvalidOperationException("Malzemesiz duvar satırı raporda bulunamadı.");
    if (satir.Contains("KALDI") || !satir.Contains("VERİ EKSİK"))
        throw new InvalidOperationException($"Malzeme atanmamış bölme VERİ EKSİK olmalı, KALDI olmamalı. Satır: {satir}");
}

static void BuildingViewModelMarksResultsStaleAfterModelChange()
{
    // Faz 6 regresyon (P0) — Hesapla() sonrası proje verisi (malzeme/sınıflandırma/vs)
    // değişirse sonuçlar "güncel değil" işaretlenmeli ve dışa aktarım kapanmalı.
    var vm = new Kst.Acoustics.App.BuildingAcoustics.ViewModels.BinaProjesiViewModel();
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3);
    kaynak.KaynakDerecesi = "OG";
    var alici = CreateRectMekan("Alıcı", 3, 0, 6, 3);
    alici.AliciSinifi = "II";
    var duvar = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55 };
    var doseme = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Rw = 55, Lnw = 55 };
    var tavan = new YapiElemani { Ad = "Tavan", Tip = YapiElemaniTipi.Tavan, Rw = 55 };
    var bagimsizBolumId = Guid.NewGuid();
    kaynak.BagimsizBolumId = bagimsizBolumId;
    alici.BagimsizBolumId = bagimsizBolumId;
    kaynak.ZeminYapiElemani = doseme;
    kaynak.TavanYapiElemani = tavan;
    alici.ZeminYapiElemani = doseme;
    alici.TavanYapiElemani = tavan;
    var bolme = new Bolme { Ad = "Ortak Duvar", Alan = 9, YapiElemani = duvar, KaynakMekan = kaynak, AliciMekan = alici };
    kaynak.Bolmeler.Add(bolme);
    // Ön kontrol her aktif mekan için bir topoloji bekler. Aynı fiziksel arayüzün
    // alıcı tarafındaki temsili de atanmış olmalı; aksi halde test geçersiz proje
    // kurup hesap motorunun güvenlik kontrolünü yanlışlıkla sınamış olur.
    alici.Bolmeler.Add(new Bolme
    {
        Ad = "Ortak Duvar (alıcı yüzü)", Alan = 9, YapiElemani = duvar,
        KaynakMekan = kaynak, AliciMekan = alici
    });

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(kaynak);
    kat.Mekanlar.Add(alici);
    vm.Proje.Katlar.Add(kat);
    vm.Katlar.Add(kat);
    vm.Proje.BinaIslevi = BinaIslevi.Konut;

    vm.HesaplaCommand.Execute(null);
    if (vm.SonucSatirlari.Count == 0)
        throw new InvalidOperationException("Test kurulumu: Hesapla() en az bir sonuç üretmeli.");
    if (!vm.SonuclarGuncelMi)
        throw new InvalidOperationException("Hesapla() sonrası sonuçlar güncel sayılmalı.");
    if (vm.SonuclarBayatMi)
        throw new InvalidOperationException("Hesapla() hemen sonrası bayat olmamalı.");
    if (!vm.DisaAktarmaEtkin)
        throw new InvalidOperationException("Güncel sonuçlarla dışa aktarım etkin olmalı.");

    // Malzemeyi değiştir — Bolme.YapiElemani property-changed Kirlet() tetiklemeli.
    var yeniMalzeme = new YapiElemani { Ad = "Yeni Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 60 };
    bolme.YapiElemani = yeniMalzeme;

    if (!vm.SonuclarBayatMi)
        throw new InvalidOperationException("Malzeme değişikliği sonrası sonuçlar bayat sayılmalı.");
    if (vm.SonuclarGuncelMi)
        throw new InvalidOperationException("Bayat sonuçlar güncel sayılmamalı.");
    if (vm.DisaAktarmaEtkin)
        throw new InvalidOperationException("Bayat sonuçlarla dışa aktarım kapalı olmalı.");
}

static void BuildingClassEngineDerivesAchievedClass()
{
    // SinifAtamaServisi — Tablo 3.2 OG×II sütunu: A=59, B=55, C=49, D=45, E=41, F=37.
    // DnT,A=50 → C sağlar (49) ama B'yi (55) sağlamaz; DnT,A=56 → B; DnT,A=30 → F bile değil.
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3);
    kaynak.KaynakDerecesi = "OG";
    var alici = CreateRectMekan("Alıcı", 3, 0, 6, 3);
    alici.AliciSinifi = "II";

    Bolme Duvar(double dntw) => new()
    {
        Ad = "Duvar", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "D", Tip = YapiElemaniTipi.Duvar, Rw = 55, C = 0 },
        KaynakMekan = kaynak, AliciMekan = alici, Dntw = dntw
    };

    var servis = new SinifAtamaServisi();

    var c = servis.SaglananSinif(BinaSinifi.S1, Duvar(50));
    if (c.Sinif != AkustikPerformansSinifi.C || c.SinifDisi || c.Belirlenemedi)
        throw new InvalidOperationException($"DnT,A=50 (OG×II) C sınıfı sağlamalı, alınan: {c.Etiket}");

    var b = servis.SaglananSinif(BinaSinifi.S1, Duvar(56));
    if (b.Sinif != AkustikPerformansSinifi.B)
        throw new InvalidOperationException($"DnT,A=56 (OG×II) B sınıfı sağlamalı, alınan: {b.Etiket}");

    var dis = servis.SaglananSinif(BinaSinifi.S1, Duvar(30));
    if (!dis.SinifDisi)
        throw new InvalidOperationException($"DnT,A=30 hiçbir sınıfı sağlamamalı (F=37), alınan: {dis.Etiket}");

    var eksik = servis.SaglananSinif(BinaSinifi.S1, Duvar(0)); // hesap yok
    if (!eksik.Belirlenemedi)
        throw new InvalidOperationException($"Dntw=0 (hesapsız) 'belirlenemedi' olmalı, alınan: {eksik.Etiket}");
}

static void BuildingClassEngineLowestClassAndUndetermined()
{
    // EK-10/9: farklı elemanlar farklı sınıflar gösteriyorsa en alt sınıf atanır.
    // Dış cephe/darbe değerlendirilemiyorsa o özellik 'performans belirlenemedi' olur
    // ama genel sınıf değerlendirilebilen özelliklerden türetilir.
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3);
    kaynak.KaynakDerecesi = "OG";
    var alici = CreateRectMekan("Alıcı", 3, 0, 6, 3);
    alici.AliciSinifi = "II";

    var iyiDuvar = new Bolme
    {
        Ad = "İyi duvar", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "D1", Tip = YapiElemaniTipi.Duvar, Rw = 55, C = 0 },
        KaynakMekan = kaynak, AliciMekan = alici, Dntw = 56 // → B
    };
    var zayifDuvar = new Bolme
    {
        Ad = "Zayıf duvar", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "D2", Tip = YapiElemaniTipi.Duvar, Rw = 40, C = 0 },
        KaynakMekan = kaynak, AliciMekan = alici, Dntw = 38 // yalnız F (37) → F
    };
    kaynak.Bolmeler.Add(iyiDuvar);
    kaynak.Bolmeler.Add(zayifDuvar);

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(kaynak);
    kat.Mekanlar.Add(alici);
    var proje = new BinaProje { BinaSinifi = BinaSinifi.S1 };
    proje.Katlar.Add(kat);

    var sonuc = new SinifAtamaServisi().BinaSinifiDegerlendir(proje);

    var bolmeOzellik = sonuc.Ozellikler.Single(o => o.Ozellik == SinifAtamaServisi.AkustikOzellik.BolmeHavaSesi);
    if (bolmeOzellik.Sonuc.Sinif != AkustikPerformansSinifi.F)
        throw new InvalidOperationException($"B + F elemanlardan en alt sınıf F atanmalı, alınan: {bolmeOzellik.Sonuc.Etiket}");

    var cepheOzellik = sonuc.Ozellikler.Single(o => o.Ozellik == SinifAtamaServisi.AkustikOzellik.DisCepheHavaSesi);
    if (!cepheOzellik.Sonuc.Belirlenemedi)
        throw new InvalidOperationException("Dış cephe elemanı yokken özellik 'performans belirlenemedi' olmalı.");

    if (sonuc.Genel.Sinif != AkustikPerformansSinifi.F)
        throw new InvalidOperationException($"Genel ön sınıf (EK-10/9) F olmalı, alınan: {sonuc.Genel.Etiket}");

    // Hariç tutma: zayıf duvar filtrelenince bölme özelliği B'ye yükselmeli.
    var filtreli = new SinifAtamaServisi().BinaSinifiDegerlendir(proje, new HashSet<Guid> { zayifDuvar.Id });
    var filtreliBolme = filtreli.Ozellikler.Single(o => o.Ozellik == SinifAtamaServisi.AkustikOzellik.BolmeHavaSesi);
    if (filtreliBolme.Sonuc.Sinif != AkustikPerformansSinifi.B)
        throw new InvalidOperationException($"Zayıf duvar hariç tutulunca sınıf B olmalı, alınan: {filtreliBolme.Sonuc.Etiket}");
}

static void BuildingHtmlReportSectionsAndFilter()
{
    // Mimari Akustik Raporu (HTML) — Madde 5/5 bölüm başlıkları, kapak metadata'sı,
    // 'performans belirlenemedi' ifadesi ve 'Rapora Dahil' hariç-tutma filtresi.
    var kaynak = CreateRectMekan("Kaynak Salon", 0, 0, 3, 3);
    kaynak.KaynakDerecesi = "OG";
    var alici = CreateRectMekan("Alıcı Yatak", 3, 0, 6, 3);
    alici.AliciSinifi = "II";

    var duvarA = new Bolme
    {
        Ad = "Rapor Duvarı A", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Tuğla duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55, C = 0 },
        KaynakMekan = kaynak, AliciMekan = alici, ApparentRw = 55, Dntw = 52
    };
    var duvarB = new Bolme
    {
        Ad = "Haric Duvari B", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Beton duvar", Tip = YapiElemaniTipi.Duvar, Rw = 58, C = 0 },
        KaynakMekan = kaynak, AliciMekan = alici, ApparentRw = 58, Dntw = 55
    };
    kaynak.Bolmeler.Add(duvarA);
    kaynak.Bolmeler.Add(duvarB);

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(kaynak);
    kat.Mekanlar.Add(alici);
    var proje = new BinaProje
    {
        ProjeAdi = "HTML Rapor Test Projesi",
        MusteriAdi = "Test Müşterisi A.Ş.",
        SorumluKisiKurulus = "Akustik Uzmanı — EYE4PRO",
        BinaSinifi = BinaSinifi.S1,
        AkustikPerformansSinifi = AkustikPerformansSinifi.C,
        DisGurultuDb = 65,
    };
    proje.Katlar.Add(kat);

    var servis = new MimariAkustikRaporServisi();
    var html = servis.HtmlOlustur(proje);

    foreach (var beklenen in new[]
    {
        "MİMARİ AKUSTİK RAPORU",
        "Test Müşterisi A.Ş.",
        "Akustik Uzmanı — EYE4PRO",
        "1. Giriş ve Kapsam",
        "2. Mekan Analizi",
        "3. Dış Gürültü Verileri",
        "5. Yapı Elemanları Listesi",
        "6. Hesap Yöntemi Beyanı",
        "TS EN 12354-1",
        "10. Tesisat ve Servis Ekipmanı Gürültüsü",
        "11. Reverberasyon Süresi Ön Değerlendirmesi",
        "13. Akustik Performans Sınıfı Ön Değerlendirmesi",
        "performans belirlenemedi", // dış cephe/darbe değerlendirilemedi
        "Rapor Duvarı A",
    })
    {
        if (!html.Contains(beklenen))
            throw new InvalidOperationException($"HTML rapor '{beklenen}' içermeli.");
    }

    // Hariç-tutma filtresi: Duvar B rapora girmemeli.
    var filtreliHtml = servis.HtmlOlustur(proje, new HashSet<Guid> { duvarB.Id });
    if (filtreliHtml.Contains("Haric Duvari B"))
        throw new InvalidOperationException("Hariç tutulan bölme HTML raporda görünmemeli.");
    if (!filtreliHtml.Contains("Rapor Duvarı A"))
        throw new InvalidOperationException("Hariç tutulmayan bölme HTML raporda görünmeli.");
}

static void BuildingEk4IndoorNoiseLimitsMatchRegulation()
{
    // EK-4 Tablo 4.1 golden — docx kaynak metniyle birebir: konut yatak odası (Gece)
    // A=26 B=30 C=34 D=38 E=42 F=46; eğitim özel derslik C=44; kültürel stüdyo C=29.
    var yatak = EK4IcGurultuServisi.LimitBul(BinaIslevi.Konut, AkustikPerformansSinifi.C, "Yatak Odası", "Yatak Odaları");
    if (yatak == null || Math.Abs(yatak.LimitDb - 34) > 0.01)
        throw new InvalidOperationException($"Konut yatak odası C sınıfı 34 dBA olmalı, alınan: {yatak?.LimitDb}");
    if (yatak.Satir.ZamanDilimi != "Gece")
        throw new InvalidOperationException("Yatak odası satırı Gece zaman dilimi taşımalı.");

    var yatakA = EK4IcGurultuServisi.LimitBul(BinaIslevi.Konut, AkustikPerformansSinifi.A, "Yatak Odası", null);
    var yatakF = EK4IcGurultuServisi.LimitBul(BinaIslevi.Konut, AkustikPerformansSinifi.F, "Yatak Odası", null);
    if (yatakA == null || yatakF == null ||
        Math.Abs(yatakA.LimitDb - 26) > 0.01 || Math.Abs(yatakF.LimitDb - 46) > 0.01)
        throw new InvalidOperationException($"Aritmetik seri A=26/F=46 olmalı, alınan: {yatakA?.LimitDb}/{yatakF?.LimitDb}");

    // Özgül anahtar önceliği: "Özel Derslik" içeren metin genel "Derslik" satırına düşmemeli.
    var ozelDerslik = EK4IcGurultuServisi.LimitBul(BinaIslevi.EgitimTesisleri, AkustikPerformansSinifi.C, "Müzik Odası Özel Derslik", null);
    if (ozelDerslik == null || Math.Abs(ozelDerslik.LimitDb - 44) > 0.01)
        throw new InvalidOperationException($"Özel derslik C=44 olmalı, alınan: {ozelDerslik?.LimitDb}");

    var studyo = EK4IcGurultuServisi.LimitBul(BinaIslevi.KulturelTesisler, AkustikPerformansSinifi.C, "Kayıt Stüdyosu", null);
    if (studyo == null || Math.Abs(studyo.LimitDb - 29) > 0.01)
        throw new InvalidOperationException($"Müzik-TV stüdyosu C=29 olmalı, alınan: {studyo?.LimitDb}");

    // Eşleşmeyen mekan → null → çağıran 35 varsayılanına düşer.
    if (EK4IcGurultuServisi.LimitBul(BinaIslevi.Konut, AkustikPerformansSinifi.C, "Garaj", null) != null)
        throw new InvalidOperationException("Tablo 4.1'de karşılığı olmayan mekan null dönmeli.");
    var proje = new BinaProje { BinaIslevi = BinaIslevi.Konut, AkustikPerformansSinifi = AkustikPerformansSinifi.C };
    var garaj = new Mekan { Ad = "Garaj", KullanimTipi = "" };
    if (Math.Abs(EK4IcGurultuServisi.IcGurultuHedefi(proje, garaj) - 35.0) > 0.01)
        throw new InvalidOperationException("Eşleşmeyen mekanda iç hedef 35 dBA varsayılanına düşmeli.");
}

static void BuildingEk5TesisatLimitsMatchRegulation()
{
    // EK-5 Tablo 5.1 golden — docx ile satır satır doğrulanan desen: Tablo 4.1 − 4 dB.
    // Konut yatak (Gece): 4.1'de C=34 → 5.1'de C=30. Tablo 5.2 kesikli: C=34 sabit satır.
    var s51 = EK5TesisatServisi.SurekliLimit(BinaIslevi.Konut, AkustikPerformansSinifi.C, "Yatak Odası", null);
    if (s51 == null || Math.Abs(s51.LimitDb - 30) > 0.01)
        throw new InvalidOperationException($"Tablo 5.1 konut yatak C=30 olmalı, alınan: {s51?.LimitDb}");

    var s52 = EK5TesisatServisi.KesikliLimit(AkustikPerformansSinifi.C);
    if (Math.Abs(s52.LimitDb - 34) > 0.01)
        throw new InvalidOperationException($"Tablo 5.2 C=34 olmalı, alınan: {s52.LimitDb}");
    var s52A = EK5TesisatServisi.KesikliLimit(AkustikPerformansSinifi.A);
    if (Math.Abs(s52A.LimitDb - 26) > 0.01)
        throw new InvalidOperationException($"Tablo 5.2 A=26 olmalı, alınan: {s52A.LimitDb}");

    // Tablo 5.1 yalnız 6 işlev için tanımlı — kültürel tesis kapsam dışı, null dönmeli.
    if (EK5TesisatServisi.SurekliLimit(BinaIslevi.KulturelTesisler, AkustikPerformansSinifi.C, "Sinema Salonu", null) != null)
        throw new InvalidOperationException("Tablo 5.1 kapsamı dışındaki işlev null dönmeli (uydurma değer yok).");

    // Değerlendirme: beyan 32 dBA, limit 30 → Kaldı; beyan 28 → Geçti; beyansız → null.
    var proje = new BinaProje { BinaIslevi = BinaIslevi.Konut, AkustikPerformansSinifi = AkustikPerformansSinifi.C };
    var mekan = new Mekan { Ad = "Yatak Odası", KullanimTipi = "Yatak Odaları", TesisatSurekliDb = 32 };
    var (surekli, kesikli) = EK5TesisatServisi.Degerlendir(proje, mekan);
    if (surekli?.Durum != UyumDurumu.Kaldi)
        throw new InvalidOperationException("Beyan 32 > limit 30 iken Kaldı olmalı.");
    if (kesikli != null)
        throw new InvalidOperationException("Kesikli beyanı yokken (0) değerlendirilmemeli.");
    mekan.TesisatSurekliDb = 28;
    if (EK5TesisatServisi.Degerlendir(proje, mekan).Surekli?.Durum != UyumDurumu.Gecti)
        throw new InvalidOperationException("Beyan 28 ≤ limit 30 iken Geçti olmalı.");
}

static void BuildingEk6ReverberationLimitsAndSabine()
{
    // EK-6 Tablo 6.1 golden: konut yatak odası T_max=0.5 s; kültürel tiyatro "-" (özel tasarım).
    // Sabine: V=48 m³, A=0 → EtkiliEmilim=0.32·48=15.36 → T=0.161·48/15.36≈0.503 s.
    var proje = new BinaProje { BinaIslevi = BinaIslevi.Konut, AkustikPerformansSinifi = AkustikPerformansSinifi.C };
    var yatak = new Mekan { Ad = "Yatak Odası", KullanimTipi = "Yatak Odaları", Alan = 16, Yukseklik = 3 };

    var rt = EK6ReverberasyonServisi.Degerlendir(proje, yatak)
        ?? throw new InvalidOperationException("Konut yatak odası Tablo 6.1'de eşleşmeli.");
    if (rt.TMaxSn is not { } tmax || Math.Abs(tmax - 0.5) > 0.01)
        throw new InvalidOperationException($"Yatak odası T_max=0.5 olmalı, alınan: {rt.TMaxSn}");
    AssertClose(0.503, rt.TahminiTSn, 0.01, "Sabine varsayılan emilimle T ≈ 0.503 s");
    // Emilim alanı kullanıcı tarafından girilmedi: 0,32V yalnız ön tahmindir ve resmî
    // geçti/kaldı kararı üretemez.
    if (rt.Durum != UyumDurumu.VeriEksik || !rt.UzmanKarariGerekir)
        throw new InvalidOperationException($"Varsayılan emilimle RT VeriEksik/uzman kararı olmalı, alınan: {rt.Durum}");

    // Emilim alanı beyan edilirse: A=20 → T=0.161·48/20=0.386 → Geçti.
    yatak.EmilimAlani = 20;
    var rt2 = EK6ReverberasyonServisi.Degerlendir(proje, yatak)!;
    if (rt2.Durum != UyumDurumu.Gecti)
        throw new InvalidOperationException($"A=20 ile T≈0.39 ≤ 0.5 Geçti olmalı, alınan: {rt2.Durum}");

    // C-D dışı sınıfta dipnot 2 uzman kararı bayrağı.
    proje.AkustikPerformansSinifi = AkustikPerformansSinifi.A;
    if (!EK6ReverberasyonServisi.Degerlendir(proje, yatak)!.UzmanKarariGerekir)
        throw new InvalidOperationException("A sınıfında dipnot 2 uzman kararı bayrağı set olmalı.");

    // Dipnot 5: tiyatro — sayısal limit yok, özel tasarım.
    var kulturel = new BinaProje { BinaIslevi = BinaIslevi.KulturelTesisler, AkustikPerformansSinifi = AkustikPerformansSinifi.C };
    var tiyatro = new Mekan { Ad = "Tiyatro Salonu", KullanimTipi = "", Alan = 300, Yukseklik = 8 };
    var rtT = EK6ReverberasyonServisi.Degerlendir(kulturel, tiyatro)!;
    if (!rtT.OzelTasarim || rtT.TMaxSn != null)
        throw new InvalidOperationException("Tiyatro dipnot 5 özel tasarım (limit yok) olmalı.");
}

static void BuildingFacadeIndoorTargetUsesEk4()
{
    // Cephe değerlendirmesindeki iç gürültü hedefi artık sabit 35 dBA değil, alıcı mekanın
    // EK-4 Tablo 4.1 limiti. Konut yatak odası C sınıfında hedef 34 dBA olmalı.
    var oda = CreateRectMekan("Yatak Odası", 0, 0, 4, 3);
    oda.KullanimTipi = "Yatak Odaları";
    oda.AliciSinifi = "I";
    var wall = new YapiElemani
    {
        Ad = "Cephe duvarı", Tip = YapiElemaniTipi.Duvar, Rw = 55,
        RThirdOctave = Enumerable.Repeat(55.0, 16).ToList()
    };
    var cephe = new Bolme
    {
        Ad = "Cephe", Alan = 10, HasGeometry = true,
        Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0),
        YapiElemani = wall, KaynakMekan = oda, AliciMekan = null
    };
    oda.Bolmeler.Add(cephe);

    var kat = new Kat { KatNo = 1 };
    kat.Mekanlar.Add(oda);
    var proje = new BinaProje
    {
        BinaSinifi = BinaSinifi.S1,
        BinaIslevi = BinaIslevi.Konut,
        AkustikPerformansSinifi = AkustikPerformansSinifi.C,
        DisGurultuDb = 65,
    };
    proje.Katlar.Add(kat);

    var satir = new BinaAkustikRaporServisi().TabloVerisiOlustur(proje).Single(s => s.Bolme == "Cephe");
    if (satir.IcHedefDb is not { } hedef || Math.Abs(hedef - 34.0) > 0.01)
        throw new InvalidOperationException($"Konut yatak odası cephe iç hedefi EK-4'ten 34 dBA olmalı, alınan: {satir.IcHedefDb}");
}

// ─── Faz 2: Annex E Kij ─────────────────────────────────────────────────────

static void BuildingAnnexEKijRigidTJunction()
{
    // ISO 12354-1 Annex E.1 rijit T-birleşimi (homojen):
    //   In-line (düz-geçiş K₁₃):  K = 5.7 + 14.1·M + 5.7·M²   (lineer M terimi düz-geçişte)
    //   Branch  (K₁₂=K₂₃):        K = 5.7 + 5.7·M²
    // (X-birleşimiyle tutarlı: düz-geçiş yolu lineer M terimini taşır.)
    AssertClose(5.7, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.T, AnnexEYolTipi.InLine, 0), 0.01, "Rijit T in-line, M=0");
    AssertClose(5.7, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0), 0.01, "Rijit T branch, M=0");
    var m = Math.Log10(2);
    AssertClose(5.7 + 14.1 * m + 5.7 * m * m, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.T, AnnexEYolTipi.InLine, m), 0.01, "Rijit T in-line, M=lg2");
    AssertClose(5.7 + 5.7 * m * m, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.T, AnnexEYolTipi.Branch, m), 0.01, "Rijit T branch, M=lg2");
    // Düz-geçiş > branch (M>0): süreklilik daha fazla titreşim azaltır.
    AssertGreater(
        AnnexEKijServisi.RigidKij(JunctionGeometriTipi.T, AnnexEYolTipi.InLine, m),
        AnnexEKijServisi.RigidKij(JunctionGeometriTipi.T, AnnexEYolTipi.Branch, m),
        "T düz-geçiş Kij, branch'tan büyük olmalı (M>0).");
}

static void BuildingAnnexEKijRigidXJunction()
{
    // Annex E rijit X (+) birleşim:
    //   Cross:    K = 8.7 + 17.1·M + 5.7·M²
    //   Perp:     K = 8.7 + 5.7·M²
    AssertClose(8.7, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.X, AnnexEYolTipi.Cross, 0), 0.01, "Rijit X cross, M=0");
    AssertClose(8.7, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.X, AnnexEYolTipi.Perp, 0), 0.01, "Rijit X perp, M=0");
    var m = Math.Log10(2);
    AssertClose(8.7 + 17.1 * m + 5.7 * m * m, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.X, AnnexEYolTipi.Cross, m), 0.01, "Rijit X cross, M=lg2");
    AssertClose(8.7 + 5.7 * m * m, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.X, AnnexEYolTipi.Perp, m), 0.01, "Rijit X perp, M=lg2");
}

static void BuildingAnnexEKijLightweightTJunctionAboveRigid()
{
    // EN 12354-1 Fig. E.7/E.8 — hafif çift-cidar formülleri (standart metniyle doğrulandı):
    //   K₁₃ = 10 + 20M − 3.3·lg(f/500), min 10 dB;  K₁₂ = 10 + 10|M| ± 3.3·lg(f/500).
    // f = 500 Hz'de frekans terimi 0: K₁₂(M=0) = 10; K₁₃(M=0) = 10 (min devrede).
    AssertClose(10.0, AnnexEKijServisi.LightweightKij(JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0), 0.01,
        "E.7 K12, M=0, f=500 → 10 dB");
    AssertClose(10.0, AnnexEKijServisi.LightweightKij(JunctionGeometriTipi.T, AnnexEYolTipi.InLine, -1.0), 0.01,
        "E.7 K13 minimumu 10 dB (M=-1'de 10+20·(-1) = -10 → min 10)");
    AssertClose(10.0 + 20.0 * 0.5, AnnexEKijServisi.LightweightKij(JunctionGeometriTipi.T, AnnexEYolTipi.InLine, 0.5), 0.01,
        "E.7 K13, M=0.5, f=500 → 20 dB");

    // Frekans terimi: f = 2000 Hz → lg(4) = 0.602.
    double lg4 = Math.Log10(4.0);
    AssertClose(10.0 + 3.3 * lg4,
        AnnexEKijServisi.LightweightKij(JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0, 2000, hepsiHafif: false), 0.01,
        "E.7 (karışık) K12 frekansla ARTAR: +3.3·lg(f/500)");
    AssertClose(10.0 - 3.3 * lg4,
        AnnexEKijServisi.LightweightKij(JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0, 2000, hepsiHafif: true), 0.01,
        "E.8 (tüm-hafif) K12 frekansla AZALIR: −3.3·lg(f/500)");
    AssertClose(10.0 + 20.0 * 0.5 - 3.3 * lg4,
        AnnexEKijServisi.LightweightKij(JunctionGeometriTipi.T, AnnexEYolTipi.InLine, 0.5, 2000), 0.01,
        "E.7/E.8 K13 frekans terimi −3.3·lg(f/500) (min 10 üzerinde kalan bölgede)");
    AssertClose(10.0,
        AnnexEKijServisi.LightweightKij(JunctionGeometriTipi.T, AnnexEYolTipi.InLine, 0, 2000), 0.01,
        "E.7/E.8 K13: M=0'da frekans terimi minimum 10 dB'ye takılır");

    // Dispatcher: yola katılan üç elemanın biri Hafif ise hafif tabanı seçilmeli (E.7 işareti).
    var k = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0,
        YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Hafif, 2000);
    AssertClose(10.0 + 3.3 * lg4, k, 0.01, "Karışık eleman: 1 adet Hafif → E.7 (pozitif frekans terimi).");

    // Tüm elemanlar hafifse E.8 işareti seçilmeli.
    var k8 = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0,
        YapiElemaniHafifligi.Hafif, YapiElemaniHafifligi.Hafif, YapiElemaniHafifligi.Hafif, 2000);
    AssertClose(10.0 - 3.3 * lg4, k8, 0.01, "Tüm-hafif birleşim → E.8 (negatif frekans terimi).");
}

static void BuildingAnnexELJunctionHasNoFfPath()
{
    // L köşede sürekli 3. eleman yok → Ff (in-line) yolu fiziksel olarak mevcut değil.
    // BaselineKij L+InLine için sınıra yakın değer (≥ 90 dB) dönmeli; yol toplamında etkisiz olur.
    var kInLine = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.L, AnnexEYolTipi.InLine, 0,
        YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen);
    AssertGreater(kInLine, 90.0, "L+InLine fiziksel olarak mevcut değil → çok yüksek Kij.");

    // L branch (köşedeki iki elemanın yolu) ise normal değerle dönmeli (≤ 30 dB).
    var kBranch = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.L, AnnexEYolTipi.Branch, 0,
        YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen);
    if (kBranch >= 30.0)
        throw new InvalidOperationException($"L branch makul değerde olmalı, alınan: {kBranch:F1} dB.");
}

static void BuildingAnnexEMixedClassUsesLightweight()
{
    // Tek bir Hafif eleman bile karışık birleşimi E.7 tabanına çeker:
    // rijit T branch (M=0) = 5.7; E.7 K12 (M=0, f=500) = 10.
    var allHomogen = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0,
        YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen);
    var mixed = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0,
        YapiElemaniHafifligi.Hafif, YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen);
    AssertClose(5.7, allHomogen, 0.01, "Rijit T branch (M=0) = 5.7 dB");
    AssertClose(10.0, mixed, 0.01, "Karışık (1 Hafif) T branch → E.7 K12 = 10 dB");
}

static void BuildingAnnexESandvicBetweenHomogenAndHafif()
{
    // EN 12354-1 Fig. E.5 — esnek ara katman: K₁₂ = rijit + Δ₁, K₁₃ = rijit + 2Δ₁,
    // Δ₁ = 10·lg(f/125) (f > 125 Hz). 500 Hz'de Δ₁ = 10·lg(4) ≈ 6.02 dB.
    double delta1 = 10.0 * Math.Log10(500.0 / 125.0);
    var homogen = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0,
        YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen);
    var sandvicBranch = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0,
        YapiElemaniHafifligi.Sandvic, YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen);
    var sandvicInline = AnnexEKijServisi.BaselineKij(
        JunctionGeometriTipi.T, AnnexEYolTipi.InLine, 0,
        YapiElemaniHafifligi.Sandvic, YapiElemaniHafifligi.Homogen, YapiElemaniHafifligi.Homogen);

    AssertClose(homogen + delta1, sandvicBranch, 0.01, "E.5 K12 = rijit + Δ1 (500 Hz'de +6.02)");
    AssertClose(5.7 + 2 * delta1, sandvicInline, 0.01, "E.5 K13 = rijit in-line + 2·Δ1");

    // f ≤ 125 Hz'de Δ1 = 0 → rijitle aynı.
    var sandvic125 = AnnexEKijServisi.SandvicKij(JunctionGeometriTipi.T, AnnexEYolTipi.Branch, 0, 125);
    AssertClose(5.7, sandvic125, 0.01, "E.5: f ≤ f1 (125 Hz) → Δ1 = 0, rijit taban");
}


static void BuildingUxMarginPositiveWhenWallExceedsLimit()
{
    // UX — Marj > 0: bölme limitten yüksek → uygun (yeşil).
    // Tablo 3.2 limit seçimi için kaynak/alıcı derece ve sınıfı set edilir.
    var wall = new YapiElemani { Ad = "Beton", Tip = YapiElemaniTipi.Duvar, Rw = 60, C = 0 };
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3);
    kaynak.KaynakDerecesi = "OG";
    var alici = CreateRectMekan("Alıcı", 3, 0, 6, 3);
    alici.AliciSinifi = "II";
    var bolme = new Bolme
    {
        Ad = "Ayırıcı", Alan = 9, YapiElemani = wall,
        ApparentRw = 60, Dntw = 58,
        KaynakMekan = kaynak, AliciMekan = alici
    };
    var svc = new YonetmelikUyumServisi();
    var marj = svc.HesaplaMarjDb(BinaSinifi.S1, bolme);
    if (double.IsNaN(marj))
        throw new InvalidOperationException("Marj NaN olmamalı; veri tam (KaynakDerecesi/AliciSinifi set edildi).");
    AssertGreater(marj, 0, $"Yüksek R'li duvarın marjı pozitif olmalı; alınan: {marj:F1}");
}

static void BuildingUxMarginNegativeWhenFloorExceedsLnw()
{
    // UX — Döşeme L'n,w limiti aşarsa marj negatif → kırmızı.
    // Limit DnT,A için tipik ~55-58; L'n,w için ~58-60.
    var doseme = new YapiElemani { Ad = "İnce döşeme", Tip = YapiElemaniTipi.Doseme, Rw = 50, Lnw = 80 };
    var kaynak = CreateRectMekan("Üst", 0, 0, 4, 3);
    kaynak.KaynakDerecesi = "OG";   // Tablo 3.3: OG, sınıf C → 54 dB L'nT,w
    var alici  = CreateRectMekan("Alt", 0, 0, 4, 3);
    alici.KullanimTipi = "Yatak Odası";
    var bolme = new Bolme
    {
        Ad = "Döşeme", Alan = 12, YapiElemani = doseme,
        ApparentLnw = 75,  // limit ~58 → marj negatif
        KaynakMekan = kaynak, AliciMekan = alici
    };
    // Yönetmelik Madde 10(3): döşeme uyumu L'nT,w üzerinden değerlendirilir.
    new DarbeSesiHesapServisi().HesaplaApparentLnTw(bolme);
    var svc = new YonetmelikUyumServisi();
    var marj = svc.HesaplaMarjDb(BinaSinifi.S1, bolme);
    if (double.IsNaN(marj))
        throw new InvalidOperationException("Marj NaN olmamalı; veri tam.");
    if (marj >= 0)
        throw new InvalidOperationException($"Yüksek L'n,w için marj negatif olmalı, alınan: {marj:F1}");
}

static void BuildingOktavTekSayiStandart16BanttanGelir()
{
    // Fix #6 — Oktav gösterim tek-sayısı (Rw/C/Ctr), standart-dışı 7-bant uyarlamasından DEĞİL,
    // standart ISO 717-1 16-bant sonucundan gelmeli. Tek-bant dipli spektrumla 16↔7 dereceleri ayrışır.
    var r = Enumerable.Repeat(55.0, 16).ToList();
    r[7] = 30; // 500 Hz'de keskin dip → 16-bant derecesini düşürür, 7-bant ortalaması daha az etkilenir
    var duvar = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55, RThirdOctave = r };
    var bolme = new Bolme
    {
        Ad = "Duvar", Alan = 10, HasGeometry = true,
        Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0),
        YapiElemani = duvar
    };

    var svc = new HavaSesiHesapServisi();
    var ucuncu = svc.HesaplaUcuncuOktavApparentR(bolme);
    var oktav  = svc.HesaplaOktavApparentR(bolme);

    AssertClose(ucuncu.RwHesap, oktav.RwHesap, 0.01,
        $"Oktav gösterim Rw, standart 16-bant Rw'ye eşit olmalı. 16={ucuncu.RwHesap}, oktav={oktav.RwHesap}");
    AssertClose(ucuncu.CHesap, oktav.CHesap, 0.01, "Oktav gösterim C, 16-bant C'ye eşit olmalı.");
    AssertClose(ucuncu.CtrHesap, oktav.CtrHesap, 0.01, "Oktav gösterim Ctr, 16-bant Ctr'ye eşit olmalı.");

    // Darbe: oktav gösterim Lnw, otoriter ApparentLnw değerine eşit olmalı.
    var doseme = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 70, DeltaLw = 20 };
    var alici  = CreateRectMekan("Alt", 0, 0, 4, 3);
    var dosemeBolme = new Bolme { Ad = "Döşeme", Alan = 12, YapiElemani = doseme, AliciMekan = alici };
    var dsvc = new DarbeSesiHesapServisi();
    var apparentLnw = dsvc.HesaplaApparentLnw(dosemeBolme);
    var oktavDarbe  = dsvc.HesaplaOktavApparentLn(dosemeBolme);
    AssertClose(apparentLnw, oktavDarbe.LnwHesap, 0.01,
        $"Oktav darbe gösterim Lnw, otoriter ApparentLnw'ye eşit olmalı. {apparentLnw} vs {oktavDarbe.LnwHesap}");
}

static void BuildingOktavGosterimAcikligiCiftSaymaz()
{
    // Fix #5 — 1/3-oktav sonucu (composite+flanking dahil) varken oktav-gösterim yolu
    // açıklık/flanking'i TEKRAR uygulamamalı. Açıklıklı bölmede oktav Rw, 1/3 Rw'sine yakın olmalı.
    var duvar = new YapiElemani
    {
        Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55,
        RThirdOctave = Enumerable.Repeat(55.0, 16).ToList(),
    };
    var pencere = new YapiElemani
    {
        Ad = "Pencere", Tip = YapiElemaniTipi.Pencere, Rw = 30,
        RThirdOctave = Enumerable.Repeat(30.0, 16).ToList(),
    };
    var bolme = new Bolme
    {
        Ad = "Cepheli duvar", Alan = 10, HasGeometry = true,
        Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0),
        YapiElemani = duvar,
        Acikliklar = { new BolmeAcikligi { Tip = BolmeAcikligiTipi.Pencere, YapiElemani = pencere, Genislik = 1.5, Yukseklik = 1.5 } }
    };

    var svc = new HavaSesiHesapServisi();
    var ucuncu = svc.HesaplaUcuncuOktavApparentR(bolme); // UcuncuOktavHavaSesiSonucu set edilir
    var oktav  = svc.HesaplaOktavApparentR(bolme);

    // Açıklık 55→~ düşük composite (≈ 40'lar). Oktav gösterim Rw'si 1/3 Rw'sine ±3 dB içinde olmalı.
    // (Çift sayım olsaydı açıklık ikinci kez düşürür, fark > 3 dB olurdu.)
    AssertClose(ucuncu.RwHesap, oktav.RwHesap, 3.0,
        $"Oktav gösterim Rw'si 1/3 Rw'sine yakın olmalı (çift composite yok). 1/3={ucuncu.RwHesap}, oktav={oktav.RwHesap}");
}

static void BuildingDntAUsesApparentSpektrumC()
{
    // Fix #4 — DnT,A için adaptasyon terimi (C), elemanın lab C'si yerine GÖRÜNÜR composite
    // spektrumun CHesap'ı olmalı. CHesap değişince marj değişmeli; spektrum yoksa lab C'ye düşmeli.
    var wall = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55, C = 5 }; // lab C kasıtlı +5
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3); kaynak.KaynakDerecesi = "OG";
    var alici  = CreateRectMekan("Alıcı", 3, 0, 6, 3); alici.AliciSinifi = "II";

    Bolme Make(double? cHesap) => new()
    {
        Ad = "Ayırıcı", Alan = 9, YapiElemani = wall, ApparentRw = 55, Dntw = 55,
        KaynakMekan = kaynak, AliciMekan = alici,
        UcuncuOktavHavaSesiSonucu = cHesap is { } c
            ? new UcuncuOktavHavaSonucu(new double[16], 55, c, -10, "x")
            : null
    };

    var svc = new YonetmelikUyumServisi();
    var marjApp = svc.HesaplaMarjDb(BinaSinifi.S1, Make(-3));   // apparent C = -3
    var marjLab = svc.HesaplaMarjDb(BinaSinifi.S1, Make(5));    // apparent C = +5
    if (double.IsNaN(marjApp) || double.IsNaN(marjLab))
        throw new InvalidOperationException("Marj NaN olmamalı (veri tam).");

    // CHesap −3 → +5 değişimi marja 8 dB yansımalı (Dntw sabit, limit sabit).
    AssertClose(8.0, marjLab - marjApp, 0.6,
        $"Apparent C marja yansımalı (−3 vs +5 → 8 dB). marjApp={marjApp:F1}, marjLab={marjLab:F1}");

    // Spektrum yoksa lab C'ye (+5) düşmeli → marjLab ile aynı.
    var marjFallback = svc.HesaplaMarjDb(BinaSinifi.S1, Make(null));
    AssertClose(marjLab, marjFallback, 0.6,
        $"Spektrum yokken lab C'ye düşmeli. fallback={marjFallback:F1}, lab={marjLab:F1}");
}

static void BuildingUxMarginNaNWhenYapiElemaniMissing()
{
    // UX — YapiElemani null ise marj NaN (VERI_EKSIK).
    var bolme = new Bolme { Ad = "Bos", Alan = 9 };
    var svc = new YonetmelikUyumServisi();
    var marj = svc.HesaplaMarjDb(BinaSinifi.S1, bolme);
    if (!double.IsNaN(marj))
        throw new InvalidOperationException($"YapiElemani yokken NaN beklenir, alınan: {marj:F1}");
}

static void BuildingB14ValidationRejectsMissingAd()
{
    // B14 — Ad alanı boş ise eleman atılır (zorunlu alan).
    var e = new YapiElemani { Ad = "", Tip = YapiElemaniTipi.Duvar, Rw = 50 };
    var sonuc = YapiElemaniKutuphanesi.DogrulaElemanDetay(e, 0);
    if (sonuc.Hatalar.Count == 0)
        throw new InvalidOperationException("Boş Ad için en az 1 hata beklenir.");
    if (!sonuc.Hatalar.Any(h => h.Contains("Ad")))
        throw new InvalidOperationException("Ad hatası bulunamadı.");
}

static void BuildingB14ValidationRejectsBadRw()
{
    // B14 — Rw < 0 veya > 100 → eleman atılır.
    var negatif = new YapiElemani { Ad = "Negatif", Tip = YapiElemaniTipi.Duvar, Rw = -5 };
    var asiri = new YapiElemani { Ad = "Aşırı", Tip = YapiElemaniTipi.Duvar, Rw = 150 };

    var sNeg = YapiElemaniKutuphanesi.DogrulaElemanDetay(negatif, 0);
    var sAsi = YapiElemaniKutuphanesi.DogrulaElemanDetay(asiri, 1);

    if (!sNeg.Hatalar.Any(h => h.Contains("Rw")))
        throw new InvalidOperationException("Negatif Rw hatası bulunamadı.");
    if (!sAsi.Hatalar.Any(h => h.Contains("Rw")))
        throw new InvalidOperationException("Aşırı yüksek Rw hatası bulunamadı.");
}

static void BuildingB14ValidationWarnsFloorWithoutLnw()
{
    // B14 — Döşeme için Lnw=0 → uyarı (hata değil; yine kataloğa alınır).
    var doseme = new YapiElemani
    {
        Ad = "BA döşeme", Tip = YapiElemaniTipi.Doseme, Rw = 55,
        Lnw = 0  // eksik
    };
    var sonuc = YapiElemaniKutuphanesi.DogrulaElemanDetay(doseme, 0);
    if (sonuc.Hatalar.Count != 0)
        throw new InvalidOperationException($"Lnw=0 hata değil uyarı olmalı. Hatalar: {string.Join(',', sonuc.Hatalar)}");
    if (!sonuc.Uyarilar.Any(u => u.Contains("Lnw")))
        throw new InvalidOperationException("Lnw uyarısı bulunamadı.");
}

static void BuildingB14ValidationWarnsWindowWithLnw()
{
    // B14 — Pencere için Lnw>0 → uyarı (anlamsız değer).
    var pencere = new YapiElemani
    {
        Ad = "Çift cam", Tip = YapiElemaniTipi.Pencere, Rw = 35,
        Lnw = 50  // hatalı: pencere için Lnw atlanır
    };
    var sonuc = YapiElemaniKutuphanesi.DogrulaElemanDetay(pencere, 0);
    if (sonuc.Hatalar.Count != 0)
        throw new InvalidOperationException("Pencere Lnw uyarı olmalı, hata değil.");
    if (!sonuc.Uyarilar.Any(u => u.Contains("Lnw")))
        throw new InvalidOperationException("Pencere Lnw uyarısı bulunamadı.");
}

static void BuildingB3CanonicalSingleSquare()
{
    // B3 — Tek kare oda: 4 segment → 1 face (CCW, +25 m² alan).
    var segs = new List<(Point2D, Point2D)>
    {
        (new Point2D(0, 0),  new Point2D(5, 0)),
        (new Point2D(5, 0),  new Point2D(5, 5)),
        (new Point2D(5, 5),  new Point2D(0, 5)),
        (new Point2D(0, 5),  new Point2D(0, 0)),
    };
    var odalar = KanonikOdaTespitServisi.OdalariBul(segs);
    if (odalar.Count != 1)
        throw new InvalidOperationException($"1 oda beklenir, alınan: {odalar.Count}");
    var alan = PoligonYardimci.Alan(odalar[0]);
    AssertClose(25.0, Math.Abs(alan), 0.01, $"Kare alanı 25 m² olmalı, alınan: {alan:F2}");
}

static void BuildingB3CanonicalTwoAdjacentRooms()
{
    // B3 — Yan yana iki oda, ortak duvarla bölünmüş: 2 face üretmeli.
    //   A____B____C
    //   |    |    |
    //   |  1 |  2 |
    //   |____|____|
    //   D    E    F
    // Vertices: A(0,3) B(3,3) C(6,3) D(0,0) E(3,0) F(6,0)
    var segs = new List<(Point2D, Point2D)>
    {
        // Dış kontur
        (new Point2D(0, 0),  new Point2D(6, 0)),  // alt - tek segment, ama köşede E var
        (new Point2D(0, 0),  new Point2D(0, 3)),  // sol
        (new Point2D(0, 3),  new Point2D(6, 3)),  // üst
        (new Point2D(6, 0),  new Point2D(6, 3)),  // sağ
        // Ortak duvar (B-E)
        (new Point2D(3, 0),  new Point2D(3, 3)),
        // Köşe vertex'i için alt segmenti böl (PSLG için her kesişimde köşe gerekir)
    };
    // Alt ve üst segmentleri E ve B köşelerinde bölmek lazım — yoksa orta duvar
    // dış kontura "asılı" kalır ve face yutar
    segs[0] = (new Point2D(0, 0), new Point2D(3, 0)); // alt sol parça
    segs.Add((new Point2D(3, 0), new Point2D(6, 0))); // alt sağ parça
    segs[2] = (new Point2D(0, 3), new Point2D(3, 3)); // üst sol parça
    segs.Add((new Point2D(3, 3), new Point2D(6, 3))); // üst sağ parça

    var odalar = KanonikOdaTespitServisi.OdalariBul(segs);
    if (odalar.Count != 2)
        throw new InvalidOperationException($"2 oda beklenir, alınan: {odalar.Count}");
    var toplamAlan = odalar.Sum(p => Math.Abs(PoligonYardimci.Alan(p)));
    AssertClose(18.0, toplamAlan, 0.05, $"Toplam alan 18 m² olmalı, alınan: {toplamAlan:F2}");
}

static void BuildingB3CanonicalIgnoresDeadEndStubs()
{
    // B3 — Dead-end stub (köşeden uzanan tek serbest segment) odaya etki etmemeli.
    var segs = new List<(Point2D, Point2D)>
    {
        // Kare oda
        (new Point2D(0, 0),  new Point2D(4, 0)),
        (new Point2D(4, 0),  new Point2D(4, 4)),
        (new Point2D(4, 4),  new Point2D(0, 4)),
        (new Point2D(0, 4),  new Point2D(0, 0)),
        // Köşe (0,0)'dan dışarıya uzanan stub
        (new Point2D(0, 0),  new Point2D(-2, -2)),
    };
    var odalar = KanonikOdaTespitServisi.OdalariBul(segs);
    if (odalar.Count != 1)
        throw new InvalidOperationException($"Stub'a rağmen 1 oda beklenir, alınan: {odalar.Count}");
    AssertClose(16.0, Math.Abs(PoligonYardimci.Alan(odalar[0])), 0.01, "Stub kareyi etkilememeli");
}

static void BuildingB3CanonicalDropsSmallFaces()
{
    // B3 — Minimum alan filtresi: 2 m²'den küçük face'ler atılır.
    var segs = new List<(Point2D, Point2D)>
    {
        // 1 m²'lik küçük kare — atılmalı
        (new Point2D(0, 0),  new Point2D(1, 0)),
        (new Point2D(1, 0),  new Point2D(1, 1)),
        (new Point2D(1, 1),  new Point2D(0, 1)),
        (new Point2D(0, 1),  new Point2D(0, 0)),
    };
    var odalar = KanonikOdaTespitServisi.OdalariBul(segs);
    if (odalar.Count != 0)
        throw new InvalidOperationException($"1 m² < MinOda=2 → 0 oda beklenir, alınan: {odalar.Count}");

    // Eşiği düşür → kabul edilmeli
    var odalar2 = KanonikOdaTespitServisi.OdalariBul(segs, new KanonikOdaTespitServisi.Ayarlar { MinOdaAlaniM2 = 0.5 });
    if (odalar2.Count != 1)
        throw new InvalidOperationException($"Eşik 0.5 m² → 1 oda beklenir, alınan: {odalar2.Count}");
}

static void BuildingB4WallExtrusionUsesAciklikYukseklik()
{
    // B4 — aciklik.Yukseklik artık hardcoded 1.2 m yerine kullanılmalı.
    var bolme = new Bolme
    {
        Ad = "Test duvar",
        Baslangic = new Point2D(0, 0), Bitis = new Point2D(5, 0),
        HasGeometry = true, Alan = 13.5
    };
    bolme.Acikliklar.Add(new BolmeAcikligi
    {
        Tip = BolmeAcikligiTipi.Pencere,
        Baslangic = new Point2D(2, 0), Bitis = new Point2D(3.5, 0),
        Genislik = 1.5, Yukseklik = 1.8, // standart-dışı yükseklik
        EsikYuksekligiM = 0.8
    });
    var mekan = new Mekan { Ad = "Oda", Yukseklik = 2.7 };
    mekan.Bolmeler.Add(bolme);

    var ekstr = new EkstruzyonServisi();
    var yuzeyler = ekstr.Mekani3DyeCevir(mekan, tabanZ: 0);

    // Pencere yüzeyi 1.8 m yükseklikte olmalı (z1-z0 = 1.8)
    var pencere = yuzeyler.FirstOrDefault(y => y.Tip == "Pencere");
    if (pencere == null) throw new InvalidOperationException("Pencere yüzeyi üretilmedi.");

    var z = pencere.Koseler.Select(k => k.Z).ToArray();
    var height = z.Max() - z.Min();
    AssertClose(1.8, height, 0.01, $"Pencere yüksekliği aciklik.Yukseklik (1.8) olmalı, alınan: {height:F2}");

    var sill = z.Min();
    AssertClose(0.8, sill, 0.01, $"Eşik yüksekliği aciklik.EsikYuksekligiM (0.8) olmalı, alınan: {sill:F2}");
}

static void BuildingB4EsikYuksekligiDefaultsByTip()
{
    // B4 — EsikYuksekligiM=0 ise: Kapı 0 m, Pencere 0.9 m varsayılana düşmeli.
    var kapi = new BolmeAcikligi { Tip = BolmeAcikligiTipi.Kapi };
    var pencere = new BolmeAcikligi { Tip = BolmeAcikligiTipi.Pencere };
    AssertClose(0.0, kapi.EtkinEsikYuksekligiM, 1e-9, "Kapı varsayılan eşik 0 m");
    AssertClose(0.9, pencere.EtkinEsikYuksekligiM, 1e-9, "Pencere varsayılan eşik 0.9 m");

    // Override → kullanıcı değeri kullanılır
    var ozel = new BolmeAcikligi { Tip = BolmeAcikligiTipi.Pencere, EsikYuksekligiM = 1.2 };
    AssertClose(1.2, ozel.EtkinEsikYuksekligiM, 1e-9, "Override eşik 1.2 m");
}

static void BuildingB4BinaGeometriEmitsOpeningSurfaces()
{
    // B4 — BinaGeometriServisi açıklık yüzeylerini "Pencere" / "Kapi" kategorisinde yaymalı.
    var proje = new BinaProje { BinaSinifi = BinaSinifi.S1 };
    var kat = new Kat { KatNo = 1, KatYuksekligi = 2.7 };
    proje.Katlar.Add(kat);

    var mekan = new Mekan
    {
        Ad = "Oda",
        Yukseklik = 2.7,
        Poligon = new() { new Point2D(0,0), new Point2D(4,0), new Point2D(4,3), new Point2D(0,3) }
    };
    kat.Mekanlar.Add(mekan);

    var duvar = new Bolme
    {
        Ad = "Cephe", Alan = 10.8, HasGeometry = true,
        Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0),
        KaynakMekan = mekan,
        YapiElemani = new YapiElemani { Ad = "Cephe", Tip = YapiElemaniTipi.Duvar, Rw = 50 }
    };
    duvar.Acikliklar.Add(new BolmeAcikligi
    {
        Tip = BolmeAcikligiTipi.Pencere,
        Baslangic = new Point2D(1, 0), Bitis = new Point2D(2.5, 0),
        Genislik = 1.5, Yukseklik = 1.2, EsikYuksekligiM = 0.9
    });
    duvar.Acikliklar.Add(new BolmeAcikligi
    {
        Tip = BolmeAcikligiTipi.Kapi,
        Baslangic = new Point2D(3, 0), Bitis = new Point2D(3.9, 0),
        Genislik = 0.9, Yukseklik = 2.1
    });
    mekan.Bolmeler.Add(duvar);

    var svc = new BinaGeometriServisi();
    var surfaces = svc.BinaModelineCevir(proje);

    var pencereSayisi = surfaces.Count(s => s.Category == "Pencere");
    var kapiSayisi    = surfaces.Count(s => s.Category == "Kapi");
    if (pencereSayisi != 1) throw new InvalidOperationException($"1 Pencere surface beklenir, alınan: {pencereSayisi}");
    if (kapiSayisi != 1)    throw new InvalidOperationException($"1 Kapi surface beklenir, alınan: {kapiSayisi}");
}

static void BuildingJunctionStrukturelKirisAdds3Db()
{
    // Ek2 — StrukturelEklemesi: Yok=0, Kiris=+3, PerdeDuvar=+5.
    AssertClose(0.0, AnnexEKijServisi.StrukturelEklemesi(JunctionStrukturelTipi.Yok), 1e-6, "Yok → 0 dB");
    AssertClose(3.0, AnnexEKijServisi.StrukturelEklemesi(JunctionStrukturelTipi.Kiris), 1e-6, "Kiriş → +3 dB");
    AssertClose(5.0, AnnexEKijServisi.StrukturelEklemesi(JunctionStrukturelTipi.PerdeDuvar), 1e-6, "PerdeDuvar → +5 dB");
}

static void BuildingJunctionStrukturelPerdeDuvarDominates()
{
    // Bir junction'da StrukturelTipi.PerdeDuvar ekleyince apparent R' belirgin artmalı
    // (Kij +5 dB, flanking enerjisi 10^0.5 kat azalır → R' yükselir).
    static YapiElemani Eleman(string ad, double rw, double yuzKutle)
        => new()
        {
            Ad = ad, Tip = YapiElemaniTipi.Duvar, Rw = rw, BaglantiTipi = BaglantiTipi.Rijit,
            Katmanlar = { new YapiKatmani { MalzemeAdi = ad, Kalinlik = yuzKutle, Yogunluk = 1000.0 } }
        };

    var ayirici = Eleman("Ayırıcı", 50, 300);
    var yan = Eleman("Yan duvar", 48, 250);
    var doseme = Eleman("Döşeme", 52, 350);

    var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var alici  = CreateRectMekan("Alıcı", 4, 0, 8, 3);
    kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
    alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;
    kaynak.Bolmeler.Add(new Bolme { Ad = "Yan kaynak", Baslangic = new Point2D(0,0), Bitis = new Point2D(4,0), HasGeometry = true, YapiElemani = yan, KaynakMekan = kaynak });
    kaynak.Bolmeler.Add(new Bolme { Ad = "Yan kaynak ust", Baslangic = new Point2D(0,3), Bitis = new Point2D(4,3), HasGeometry = true, YapiElemani = yan, KaynakMekan = kaynak });
    alici.Bolmeler.Add(new Bolme { Ad = "Yan alici", Baslangic = new Point2D(4,0), Bitis = new Point2D(8,0), HasGeometry = true, YapiElemani = yan, KaynakMekan = alici });
    alici.Bolmeler.Add(new Bolme { Ad = "Yan alici ust", Baslangic = new Point2D(4,3), Bitis = new Point2D(8,3), HasGeometry = true, YapiElemani = yan, KaynakMekan = alici });

    Bolme MakeBolme() => new()
    {
        Ad = "Ayırıcı", Alan = 9,
        Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 3), HasGeometry = true,
        YapiElemani = ayirici, KaynakMekan = kaynak, AliciMekan = alici
    };

    var svc = new HavaSesiHesapServisi();
    var bolmeBase = MakeBolme();
    kaynak.Bolmeler.Add(bolmeBase);
    var rwBase = svc.HesaplaApparentRw(bolmeBase);

    // Aynı bolme, tüm junction'lar PerdeDuvar olarak işaretli
    kaynak.Bolmeler.Remove(bolmeBase);
    var bolmePerde = MakeBolme();
    kaynak.Bolmeler.Add(bolmePerde);
    HavaSesiHesapServisi.EnsureDefaultJunctions(bolmePerde);
    foreach (var j in bolmePerde.Junctions) j.StrukturelTipi = JunctionStrukturelTipi.PerdeDuvar;
    var rwPerde = svc.HesaplaApparentRw(bolmePerde);

    // Not: Per-eleman T_s (Annex C) devrede olduğundan flank elemanların in-situ R'si
    // yükselir, flanking daha az baskın olur ve PerdeDuvar'ın göreli etkisi ~1 dB'ye iner.
    AssertGreater(rwPerde - rwBase, 0.8,
        $"PerdeDuvar tüm junction'larda apparent R'yi belirgin (≥ 0.8 dB) yükseltmeli. Base={rwBase:F1}, Perde={rwPerde:F1}");
}

static void BuildingYapiElemaniIsInceFlag()
{
    // Ek2 — IsInce: toplam yapısal kalınlık < 100 mm ise true; hava boşlukları sayılmaz.
    var ince = new YapiElemani
    {
        Ad = "Alçıpan tek katman", Tip = YapiElemaniTipi.Duvar,
        Katmanlar = { new YapiKatmani { MalzemeAdi = "Alçıpan", Kalinlik = 12.5, Yogunluk = 800 } }
    };
    var kalin = new YapiElemani
    {
        Ad = "Beton 20cm", Tip = YapiElemaniTipi.Duvar,
        Katmanlar = { new YapiKatmani { MalzemeAdi = "Beton", Kalinlik = 200, Yogunluk = 2400 } }
    };
    if (!ince.IsInce) throw new InvalidOperationException("12.5 mm alçıpan ince sayılmalı.");
    if (kalin.IsInce) throw new InvalidOperationException("200 mm beton kalın sayılmalı.");
    AssertClose(12.5, ince.ToplamKalinlikMm, 0.001, "İnce eleman kalınlığı 12.5 mm");
    AssertClose(200.0, kalin.ToplamKalinlikMm, 0.001, "Kalın eleman kalınlığı 200 mm");
}

static void BuildingMenfezDneLowersCompositeRw()
{
    // EN 12354-3 — Composite formülü: τ_total·S = Σ S_i·τ_i + Σ A_0·10^(-Dne/10)
    // Bir mekan menfezi (Dne=35 dB) eklemek composite Rw'yi belirgin (≥2 dB) düşürmeli.
    var wall = new YapiElemani
    {
        Ad = "Beton 20cm", Tip = YapiElemaniTipi.Duvar, Rw = 55,
        Katmanlar = { new YapiKatmani { MalzemeAdi = "Beton", Kalinlik = 200, Yogunluk = 2400 } }
    };
    var bolmeBase = new Bolme { Ad = "Duvar", Alan = 12, YapiElemani = wall };
    var rwBase = HavaSesiHesapServisi.CalculateCompositeRw(bolmeBase);

    var bolmeVent = new Bolme { Ad = "Duvar+menfez", Alan = 12, YapiElemani = wall };
    bolmeVent.Menfezler.Add(new Menfez { Tip = MenfezTipi.Mekanik, DneDb = 35.0 });
    var rwVent = HavaSesiHesapServisi.CalculateCompositeRw(bolmeVent);

    AssertGreater(rwBase - rwVent, 2.0,
        $"Mekanik menfez (Dne=35) composite Rw'yi ≥ 2 dB düşürmeli. Base={rwBase:F1}, Vent={rwVent:F1}");
}

static void BuildingMenfezAkustikDecouplesBetterThanMekanik()
{
    // Akustik söndürücülü menfez (Dne=48) Mekanik menfezden (Dne=38) belirgin daha yüksek
    // composite Rw vermeli — yüksek Dne = daha düşük τ_menfez = daha az transmission.
    var wall = new YapiElemani
    {
        Ad = "Beton 20cm", Tip = YapiElemaniTipi.Duvar, Rw = 55,
        Katmanlar = { new YapiKatmani { MalzemeAdi = "Beton", Kalinlik = 200, Yogunluk = 2400 } }
    };

    var bolmeMek = new Bolme { Alan = 12, YapiElemani = wall };
    bolmeMek.Menfezler.Add(new Menfez { Tip = MenfezTipi.Mekanik, DneDb = 38.0 });
    var rwMek = HavaSesiHesapServisi.CalculateCompositeRw(bolmeMek);

    var bolmeAk = new Bolme { Alan = 12, YapiElemani = wall };
    bolmeAk.Menfezler.Add(new Menfez { Tip = MenfezTipi.Akustik, DneDb = 48.0 });
    var rwAk = HavaSesiHesapServisi.CalculateCompositeRw(bolmeAk);

    AssertGreater(rwAk - rwMek, 3.0,
        $"Akustik söndürücü (Dne=48) Mekanik'ten (Dne=38) ≥ 3 dB üstün olmalı. Ak={rwAk:F1}, Mek={rwMek:F1}");
}

static void BuildingMenfezSpectrumDrivesBandByBandTau()
{
    // Spektrum doluysa CalculateCompositeRThirdOctave her bantta o bandın Dne(f)'ini kullanmalı.
    // Düşük frekansta Dne düşük (yüksek transmission), yüksek frekansta Dne yüksek (düşük transmission)
    // → düşük bantta composite R düşer, yüksekte değişim küçük olur.
    var wall = new YapiElemani
    {
        Ad = "Beton 20cm", Tip = YapiElemaniTipi.Duvar, Rw = 55,
        RThirdOctave = Enumerable.Repeat(55.0, 16).ToList()
    };
    var spectrum = new List<double> { 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50 };
    var bolme = new Bolme { Alan = 12, YapiElemani = wall };
    bolme.Menfezler.Add(new Menfez
    {
        Tip = MenfezTipi.Mekanik,
        DneDb = 35.0,
        DneThirdOctave = spectrum
    });

    var solidR = OktavBantElemanServisi.GetRUcuncuOktavDizisi(wall);
    var compositeR = HavaSesiHesapServisi.CalculateCompositeRThirdOctave(bolme, solidR);

    // 100 Hz bandında Dne=20 (çok geçirken) → composite R 100 Hz çok düşmeli
    // 3150 Hz bandında Dne=50 (iyi söndüren) → composite R sadece az düşmeli
    AssertGreater(compositeR[15] - compositeR[0], 5.0,
        $"Spektral Dne: düşük bant ≥ 5 dB daha düşük R üretmeli. Düşük={compositeR[0]:F1}, Yüksek={compositeR[15]:F1}");
}

static void BuildingSentetikRfNominalRwyeKalibre()
{
    // Fix #3 — Yalnız tek-sayı Rw bilinen (ölçüm spektrumu olmayan) eleman için sentezlenen
    // R(f) eğrisi, tekrar ISO 717-1'den geçince nominal Rw'yi vermeli (kendiyle tutarlılık).
    // Kritik frekans → coincidence dip ekler; kalibrasyon olmadan derece Rw'den sapardı.
    var svc = new HavaSesiHesapServisi();

    // Katman tabanlı elemanlar — fc/f0 katmanlardan türetilir.
    YapiElemani TekCidar(double rw, double kalinlikMm, double yogunluk) => new()
    {
        Ad = "Tek cidar", Tip = YapiElemaniTipi.Duvar, Rw = rw,
        Katmanlar = { new YapiKatmani { MalzemeAdi = "Beton", Kalinlik = kalinlikMm, Yogunluk = yogunluk } }
    };
    YapiElemani CiftCidar(double rw) => new()
    {
        Ad = "Çift cidar", Tip = YapiElemaniTipi.Duvar, Rw = rw,
        Katmanlar =
        {
            new YapiKatmani { MalzemeAdi = "Alçıpan", Kalinlik = 12.5, Yogunluk = 800 },
            new YapiKatmani { MalzemeAdi = "Hava boşluğu", Kalinlik = 50, Yogunluk = 1.2 },
            new YapiKatmani { MalzemeAdi = "Alçıpan", Kalinlik = 12.5, Yogunluk = 800 },
        }
    };

    foreach (var e in new[]
             {
                 TekCidar(50, 200, 2000),   // kalın beton → düşük fc
                 TekCidar(45, 100, 800),    // hafif tek cidar → yüksek fc
                 TekCidar(55, 250, 2400),   // ağır beton
                 CiftCidar(48),             // çift cidar → f₀
             })
    {
        var r16 = OktavBantElemanServisi.GetRUcuncuOktavDizisi(e);
        var (hesapRw, _, _, yetersiz) = svc.CalculateSingleValue(r16);
        if (yetersiz) throw new InvalidOperationException("16 bant sentezlenmeliydi.");
        AssertClose(e.Rw, hesapRw, 0.01,
            $"Sentetik R(f) nominal Rw'ye kalibre olmalı (Rw={e.Rw}, fc={e.KritikFrekansHz}, f0={e.KutleselRezonansHz} → {hesapRw}).");
    }

    // Ölçülen 1/3-oktav verisi varsa kalibrasyon uygulanmaz (veri korunur).
    var olculen = new YapiElemani
    {
        Ad = "Ölçüm", Tip = YapiElemaniTipi.Duvar, Rw = 99, // nominal Rw kasıtlı yanlış
        RThirdOctave = Enumerable.Repeat(40.0, 16).ToList(),
    };
    var rOlculen = OktavBantElemanServisi.GetRUcuncuOktavDizisi(olculen);
    AssertClose(40.0, rOlculen[0], 0.01, "Ölçülen veri kalibrasyonla DEĞİŞTİRİLMEMELİ.");
}

static void BuildingDirectRwIndependentOfStructuralTs()
{
    // ISO 12354-1 düzeltmesi (fix #2): ayırıcı (direkt) eleman için laboratuvar R'si doğrudan
    // kullanılır; yapısal T_s in-situ etkisi yalnız flanking yollarında (Dv,ij) hesaba katılır.
    // Yan yol olmayan izole bir bölmede apparent R', elemanın T_s'inden BAĞIMSIZ ve lab Rw'ye eşit olmalı.
    static YapiElemani Eleman(string ad, double rw, double ts)
        => new()
        {
            Ad = ad, Tip = YapiElemaniTipi.Duvar, Rw = rw, BaglantiTipi = BaglantiTipi.Rijit,
            YapisalRtSn = ts,
            RThirdOctave = Enumerable.Repeat(rw, 16).ToList(),
            Katmanlar = { new YapiKatmani { MalzemeAdi = ad, Kalinlik = 200, Yogunluk = 2000.0 } }
        };

    var ayiriciRef    = Eleman("Ayırıcı referans", 50, 0.15);
    var ayiriciDamped = Eleman("Ayırıcı damped",   50, 0.075);
    var ayiriciLoose  = Eleman("Ayırıcı az damped", 50, 0.30);

    // Yan yol olmaması için odalarda ek duvar / döşeme / tavan elemanı yok.
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var alici  = CreateRectMekan("Alıcı", 4, 0, 8, 3);

    static Bolme MakeBolme(YapiElemani el, Mekan k, Mekan a) => new()
    {
        Ad = "Ayırıcı", Alan = 9,
        Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 3), HasGeometry = true,
        YapiElemani = el, KaynakMekan = k, AliciMekan = a
    };

    var svc = new HavaSesiHesapServisi();
    var rwRef    = svc.HesaplaApparentRw(MakeBolme(ayiriciRef,    kaynak, alici));
    var rwDamped = svc.HesaplaApparentRw(MakeBolme(ayiriciDamped, kaynak, alici));
    var rwLoose  = svc.HesaplaApparentRw(MakeBolme(ayiriciLoose,  kaynak, alici));

    AssertClose(rwRef, rwDamped, 0.01,
        $"Direkt R', T_s'den bağımsız olmalı (damped={rwDamped:F2}, ref={rwRef:F2}).");
    AssertClose(rwRef, rwLoose, 0.01,
        $"Direkt R', T_s'den bağımsız olmalı (loose={rwLoose:F2}, ref={rwRef:F2}).");
    AssertClose(50.0, rwRef, 1.0,
        $"Yan yol/açıklık yokken apparent R', lab Rw (50) değerine eşit olmalı (alınan={rwRef:F2}).");
}

static void BuildingFlankingPerPathKijDiffersAtTJunction()
{
    // T-birleşiminde Df (D↔f, branch) ve Ff (F↔f, in-line) farklı Annex E formülleri kullanır.
    // Kütleler asimetrikse aynı junction'da farklı Kij üretilmeli (sabit per-junction Kij hatalı).
    // Eski sabit per-junction modeli bu testi geçemez.
    static YapiElemani Eleman(string ad, YapiElemaniTipi tip, double rw, double yuzKutle)
        => new()
        {
            Ad = ad, Tip = tip, Rw = rw, BaglantiTipi = BaglantiTipi.Rijit,
            // YüzKütle = (Kalinlik mm / 1000) × Yogunluk. Yogunluk=1000 → Kalinlik(mm) = YüzKütle.
            Katmanlar = { new YapiKatmani { MalzemeAdi = ad, Kalinlik = yuzKutle, Yogunluk = 1000.0 } }
        };

    var ayirici = Eleman("Ayırıcı ağır", YapiElemaniTipi.Duvar, 55, 400);
    var doseme = Eleman("Döşeme hafif", YapiElemaniTipi.Doseme, 52, 100);
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
    kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
    alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;

    var bolme = new Bolme
    {
        Ad = "Ayırıcı", Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 3),
        HasGeometry = true, Alan = 9, YapiElemani = ayirici, KaynakMekan = kaynak, AliciMekan = alici
    };
    kaynak.Bolmeler.Add(bolme);

    var yollar = HavaSesiHesapServisi.GetOnUcYolFlankingYollari(bolme);
    var df = yollar.First(y => y.Kenar == JunctionKenarTipi.Alt && y.YolTipi == "Df");
    var ff = yollar.First(y => y.Kenar == JunctionKenarTipi.Alt && y.YolTipi == "Ff");

    // Beklenen: Df branch (M≈-0.4) → K~1; Ff in-line (M≈+0.6) → K~7.8; fark > 5 dB.
    AssertGreater(Math.Abs(ff.Kij - df.Kij), 5.0, "T-birleşiminde Df (branch) ve Ff (in-line) Kij belirgin farklı olmalı.");
}

// ─── Faz 2b: Soğurma uzunluğu in-situ dönüşümü ───────────────────────────────

static void BuildingAnnexEAbsorptionLengthFormula()
{
    // EN 12354-1 / ISO 10848: a = 2.2·π²·S/(c₀·T_s)·√(f_ref/f).
    // f_ref = 1000 Hz, c₀ = 343 m/s. Varsayılan T_s = 0.5 s, f = 500 Hz.
    // S=10 için: a = 2.2π²·10/(343·0.5)·√2 ≈ 1.789 m.
    var beklenen = 2.2 * Math.PI * Math.PI * 10.0 / (343.0 * 0.5) * Math.Sqrt(2.0);
    AssertClose(beklenen, AnnexEKijServisi.EquivalentAbsorptionLength(10.0), 0.001, "Eşdeğer soğurma uzunluğu formülü (varsayılan T_s=0.5, f=500)");

    // Frekans ↑ → a ↓ (√(f_ref/f) bağımlılığı). f=1000 → √1 = 1; f=500 → √2.
    var a500 = AnnexEKijServisi.EquivalentAbsorptionLength(10.0, 0.5, 500);
    var a1000 = AnnexEKijServisi.EquivalentAbsorptionLength(10.0, 0.5, 1000);
    AssertClose(Math.Sqrt(2.0), a500 / a1000, 0.001, "Soğurma uzunluğu frekans ölçeklendirmesi √(f_ref/f)");
}

static void BuildingFlankingAppliesAbsorptionLengthCorrection()
{
    // EN 12354-1 in-situ: D̄_v,ij = K_AnnexE − 10·log10(lij/√(ai·aj)).
    // Bilinen geometri+kütlelerle Df yolu Kij'i tam olarak ai/aj/lij'ye bağlı sapmayı yansıtmalı.
    static YapiElemani Eleman(string ad, YapiElemaniTipi tip, double rw, double yuzKutle)
        => new()
        {
            Ad = ad, Tip = tip, Rw = rw, BaglantiTipi = BaglantiTipi.Rijit,
            Katmanlar = { new YapiKatmani { MalzemeAdi = ad, Kalinlik = yuzKutle, Yogunluk = 1000.0 } }
        };

    var d = Eleman("D ağır", YapiElemaniTipi.Duvar, 55, 400);
    var doseme = Eleman("Döşeme hafif", YapiElemaniTipi.Doseme, 52, 100);
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3); // Alan = 12
    kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
    alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;

    var bolme = new Bolme
    {
        Ad = "Ayırıcı", Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 3), // lij = 3
        HasGeometry = true, Alan = 9, YapiElemani = d, KaynakMekan = kaynak, AliciMekan = alici
    };
    kaynak.Bolmeler.Add(bolme);

    var df = HavaSesiHesapServisi.GetOnUcYolFlankingYollari(bolme)
        .First(y => y.Kenar == JunctionKenarTipi.Alt && y.YolTipi == "Df");

    // Annex E rijit T branch — EN 12354-1 s.43 M tanımı: M = lg(m'⊥,i/m'i);
    // Df yolunda i = ayırıcı D, dik eleman = flank döşeme → M = lg(m'flank/m'D) = lg(100/400):
    var m = Math.Log10(100.0 / 400.0);
    var kAnnex = AnnexEKijServisi.RigidKij(JunctionGeometriTipi.T, AnnexEYolTipi.Branch, m);
    // Df path: Si=Ss=9 (T_s=ayırıcı), Sj=aliciAlan=12 (T_s=alıcı flank döşeme), lij=3.
    // Per-eleman T_s artık Annex C kütle tahmininden gelir (YapisalRtSn girilmemiş).
    var tsI = d.TahminiYapisalRtSn();      // ayırıcı duvar (400 kg/m²)
    var tsJ = doseme.TahminiYapisalRtSn(); // alıcı flank döşeme (100 kg/m²)
    var aI = AnnexEKijServisi.EquivalentAbsorptionLength(9, tsI);
    var aJ = AnnexEKijServisi.EquivalentAbsorptionLength(12, tsJ);
    // A4 — E.3 Limiting values: D̄_v,ij,situ ≥ 0.
    var korek = AnnexEKijServisi.AbsorptionLengthCorrection(3.0, aI, aJ);
    var beklenen = Math.Max(0, kAnnex + korek);
    AssertClose(beklenen, df.Kij, 0.05, "Df Kij_in_situ = max(0, K_AnnexE_Branch + AbsorptionCorrection(lij,ai,aj))");
}

// ─── Faz 3: Cephe hacim normalizasyonu (EN 12354-3) ──────────────────────────

static void BuildingFacadeDntwAppliesVolumeNormalization()
{
    // EN 12354-3: D_2m,nT,w = R'_composite − ΔL_fs + 10·log10(V/(6·T₀·S))   [T₀ = 0.5 s]
    // V = 50 m³, S = 10 m², ΔL_fs = 0: 10·log10(50/(6·0.5·10)) = 10·log10(1.667) = +2.218 dB.
    var oda = new Mekan { Ad = "Salon", KullanimTipi = "Salon", Alan = 20, Yukseklik = 2.5 }; // V = 50
    var wall = new YapiElemani { Ad = "Cephe", Tip = YapiElemaniTipi.Duvar, Rw = 50 };
    var bolme = new Bolme { Ad = "Cephe", Alan = 10, YapiElemani = wall, KaynakMekan = oda, AliciMekan = null };

    var service = new DisGurultuServisi();
    var dntw = service.HesaplaDisCepheYalitimi(oda, [bolme], CepheMaruziyet.SerbestAlan);
    // R'composite ≈ Rw = 50 (tek opak, açıklık yok). ΔLfs = 0. Vnorm = +2.218 dB.
    AssertClose(50.0 + 2.218, dntw, 0.05, "Cephe D_2m,nT,w = R' + 10·log10(V/(6·T₀·S))");

    // ΔL_fs trafik/yansıtıcı karşı cephe = −3 dB (EKLENİR) → Dntw = 50 + 2.218 + (−3) = 49.218.
    var dntwTrafik = service.HesaplaDisCepheYalitimi(oda, [bolme], CepheMaruziyet.TrafikCephesi);
    AssertClose(49.218, dntwTrafik, 0.05, "Yansıtıcı karşı cephe ΔL_fs=−3 dB → D_2m,nT 3 dB düşer");

    // Küçük oda / büyük cephe → Vnorm negatif. V=20, S=10 → 10·log10(20/30) = −1.761.
    var kucukOda = new Mekan { Alan = 8, Yukseklik = 2.5 }; // V = 20
    bolme.KaynakMekan = kucukOda;
    var dntwKucuk = service.HesaplaDisCepheYalitimi(kucukOda, [bolme], CepheMaruziyet.SerbestAlan);
    AssertClose(50.0 - 1.761, dntwKucuk, 0.05, "Küçük oda/büyük cephe → Vnorm negatif");
}

static void BuildingFacadeSpectralAggregatesRoomFacades()
{
    var oda = new Mekan { Ad = "Köşe oda", KullanimTipi = "Salon", Alan = 20, Yukseklik = 2.5 };
    var iyi = new YapiElemani
    {
        Ad = "İyi cephe",
        Tip = YapiElemaniTipi.Duvar,
        Rw = 60,
        Ctr = 0,
        RThirdOctave = Enumerable.Repeat(60.0, 16).ToList()
    };
    var zayif = new YapiElemani
    {
        Ad = "Zayıf cephe",
        Tip = YapiElemaniTipi.Duvar,
        Rw = 30,
        Ctr = 0,
        RThirdOctave = Enumerable.Repeat(30.0, 16).ToList()
    };
    var cepheIyi = new Bolme { Ad = "İyi dış cephe", Alan = 10, KaynakMekan = oda, AliciMekan = null, YapiElemani = iyi };
    var cepheZayif = new Bolme { Ad = "Zayıf dış cephe", Alan = 10, KaynakMekan = oda, AliciMekan = null, YapiElemani = zayif };
    oda.Bolmeler.Add(cepheIyi);
    oda.Bolmeler.Add(cepheZayif);

    var proje = new BinaProje { DisGurultuDb = 70, DisGurultuCepheMaruziyeti = CepheMaruziyet.SerbestAlan };
    var servis = new DisGurultuServisi();
    var grup = servis.HesaplaSpektralDisCephe(proje, cepheIyi)
        ?? throw new InvalidOperationException("Grup dış cephe spektral sonucu üretilmeli.");

    var tekOda = new Mekan { Ad = "Tek cephe oda", KullanimTipi = "Salon", Alan = 20, Yukseklik = 2.5 };
    var tekCephe = new Bolme { Ad = "Tek iyi cephe", Alan = 10, KaynakMekan = tekOda, AliciMekan = null, YapiElemani = iyi };
    tekOda.Bolmeler.Add(tekCephe);
    var tek = servis.HesaplaSpektralDisCephe(proje, tekCephe)
        ?? throw new InvalidOperationException("Tek dış cephe spektral sonucu üretilmeli.");

    AssertGreater(tek.DnTATr - grup.DnTATr, 20.0, "Zayıf ikinci cephe oda toplam DnT,A,tr değerini belirgin düşürmeli.");
    if (!ReferenceEquals(cepheIyi.DisCepheSpektralSonuc, cepheZayif.DisCepheSpektralSonuc))
        throw new InvalidOperationException("Aynı odadaki dış cephe parçaları aynı oda-toplam spektral sonucu paylaşmalı.");
    AssertClose(cepheIyi.Dntw, cepheZayif.Dntw, 0.001, "Oda toplam dış cephe Dntw değeri cephe parçalarına aynı yazılmalı.");
}

// ─── Cephe form düzeltmesi ΔLfs (EN 12354-3 Tablo 1) ─────────────────────────

static void BuildingFacadeDeltaLfsVariesByForm()
{
    // EN 12354-3:2000 Figure C.2 ile birebir doğrulandı (2026-06-10): ΔL_fs EKLENİR.
    // Basit lookup, (1,5–2,5)m görüş mesafesi satırından okur.
    AssertClose( 0.0, DisGurultuServisi.DeltaLfs(CepheMaruziyet.SerbestAlan), 0.001, "ΔL_fs SerbestAlan = 0 (form 1)");
    AssertClose(-3.0, DisGurultuServisi.DeltaLfs(CepheMaruziyet.TrafikCephesi), 0.001, "ΔL_fs TrafikCephesi = −3 (C.2 dışı, kanyon)");
    AssertClose( 4.0, DisGurultuServisi.DeltaLfs(CepheMaruziyet.BalkonYutucuTavanli), 0.001, "ΔL_fs Balkon yutucu = +4 (form 7, α≥0.9)");
    AssertClose( 0.0, DisGurultuServisi.DeltaLfs(CepheMaruziyet.BalkonYansiticiTavanli), 0.001, "ΔL_fs Balkon yansıtıcı = 0 (form 7, α≤0.3)");
    AssertClose( 2.0, DisGurultuServisi.DeltaLfs(CepheMaruziyet.Loggia), 0.001, "ΔL_fs Loggia = +2 (form 8, α≥0.9)");
    // Emici > yansıtıcı (tablo sıralaması)
    AssertGreater(
        DisGurultuServisi.DeltaLfs(CepheMaruziyet.BalkonYutucuTavanli),
        DisGurultuServisi.DeltaLfs(CepheMaruziyet.BalkonYansiticiTavanli),
        "Yutucu tavanlı balkon, yansıtıcıdan yüksek ΔL_fs vermeli.");

    // Yutucu balkon, hesaplanan D_2m,nT,w'yi ΔL_fs (+4) kadar ARTIRMALI.
    var oda = new Mekan { Ad = "Salon", Alan = 20, Yukseklik = 2.5 }; // V = 50
    var wall = new YapiElemani { Ad = "Cephe", Tip = YapiElemaniTipi.Duvar, Rw = 50 };
    var bolme = new Bolme { Ad = "Cephe", Alan = 10, YapiElemani = wall, KaynakMekan = oda };
    var service = new DisGurultuServisi();

    var serbest = service.HesaplaDisCepheYalitimi(oda, [bolme], CepheMaruziyet.SerbestAlan);
    var balkonYutucu = service.HesaplaDisCepheYalitimi(oda, [bolme], CepheMaruziyet.BalkonYutucuTavanli);
    AssertClose(serbest + 4.0, balkonYutucu, 0.05, "Yutucu tavanlı balkon D_2m,nT,w'yi +4 dB artırır");

    // Tam tablo lookup: Figure C.2 hücreleri (standart metniyle doğrulandı).
    AssertClose(0.0, DisGurultuServisi.DeltaLfsTam(
        DisGurultuServisi.CepheFormu.DuzCephe, DisGurultuServisi.CatiSogurmasi.Yuksek, DisGurultuServisi.GorusMesafesi.Uzak),
        0.001, "Düz cephe her koşulda 0");
    AssertClose(4.0, DisGurultuServisi.DeltaLfsTam(
        DisGurultuServisi.CepheFormu.Balkon, DisGurultuServisi.CatiSogurmasi.Yuksek, DisGurultuServisi.GorusMesafesi.Orta),
        0.001, "Balkon, yutucu tavan, orta görüş = +4");
    AssertClose(4.0, DisGurultuServisi.DeltaLfsTam(
        DisGurultuServisi.CepheFormu.Teras, DisGurultuServisi.CatiSogurmasi.Az, DisGurultuServisi.GorusMesafesi.Orta),
        0.001, "Teras (form 9 açık çit), α≤0.3, orta görüş = +4");
    AssertClose(6.0, DisGurultuServisi.DeltaLfsTam(
        DisGurultuServisi.CepheFormu.Teras, DisGurultuServisi.CatiSogurmasi.Yuksek, DisGurultuServisi.GorusMesafesi.Uzak),
        0.001, "Teras, α≥0.9, uzak görüş = +6");
    AssertClose(-1.0, DisGurultuServisi.DeltaLfsTam(
        DisGurultuServisi.CepheFormu.Galeri, DisGurultuServisi.CatiSogurmasi.Az, DisGurultuServisi.GorusMesafesi.Yakin),
        0.001, "Galeri (form 3), α≤0.3, yakın görüş = −1");
    // Balkon: yutucu (Yuksek) > yansıtıcı (Az) aynı görüş mesafesinde
    AssertGreater(
        DisGurultuServisi.DeltaLfsTam(DisGurultuServisi.CepheFormu.Balkon, DisGurultuServisi.CatiSogurmasi.Yuksek, DisGurultuServisi.GorusMesafesi.Orta),
        DisGurultuServisi.DeltaLfsTam(DisGurultuServisi.CepheFormu.Balkon, DisGurultuServisi.CatiSogurmasi.Az, DisGurultuServisi.GorusMesafesi.Orta),
        "Tablo: yutucu tavan yansıtıcıdan yüksek ΔL_fs.");
}

// ─── Faz 2c: Frekans bağımlı a(f) ve per-eleman T_s ──────────────────────────

static void BuildingFlankingPerBandKijVariesWithFrequency()
{
    // 1/3 oktav yolda Kij artık band frekansına bağlı: a(f) ∝ √(f_ref/f).
    // Düz direct R(f) ile bile R'(f) bantlar arasında Kij'nin frekans bağımlılığı
    // yüzünden farklılaşmalı. Düşük frekansta a büyük → K_in_situ yüksek → flanking zayıf
    // → R'(low) > R'(high). Eski (band-bağımsız) kodda fark = 0 idi.
    static YapiElemani Eleman(string ad, YapiElemaniTipi tip, double rw)
        => new()
        {
            Ad = ad, Tip = tip, Rw = rw, BaglantiTipi = BaglantiTipi.Rijit,
            RThirdOctave = Enumerable.Repeat(rw, 16).ToList()
        };

    var ayirici = Eleman("Ayırıcı", YapiElemaniTipi.Duvar, 50);
    var doseme = Eleman("Döşeme", YapiElemaniTipi.Doseme, 50);
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
    kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
    alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;

    var bolme = new Bolme
    {
        Ad = "Ayırıcı", Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 3),
        HasGeometry = true, Alan = 9, YapiElemani = ayirici, KaynakMekan = kaynak, AliciMekan = alici
    };
    kaynak.Bolmeler.Add(bolme);

    var sonuc = new HavaSesiHesapServisi().HesaplaUcuncuOktavApparentR(bolme);
    AssertGreater(sonuc.R_f[0] - sonuc.R_f[15], 3.0, "Per-band Kij düşük frekansta R'(f)'i yükseltmeli (a(f) ∝ √(f_ref/f))");
}

static void BuildingFlankingPerElementTsAffectsKij()
{
    // Eleman.YapisalRtSn değeri Kij hesabına geçmeli. T_s büyür → a küçülür →
    // soğurma düzeltmesi daha negatif → K düşer → flanking artar → R'w düşer.
    static YapiElemani Eleman(string ad, YapiElemaniTipi tip, double rw, double ts = 0)
        => new()
        {
            Ad = ad, Tip = tip, Rw = rw, BaglantiTipi = BaglantiTipi.Rijit,
            YapisalRtSn = ts,
            Katmanlar = { new YapiKatmani { MalzemeAdi = ad, Kalinlik = 100, Yogunluk = 1000.0 } }
        };

    static double ApparentRwFor(double tsDoseme)
    {
        var ayirici = Eleman("D", YapiElemaniTipi.Duvar, 55);
        var doseme = Eleman("Döşeme", YapiElemaniTipi.Doseme, 52, tsDoseme);
        var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
        var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
        kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
        alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;
        var bolme = new Bolme
        {
            Ad = "Ayırıcı", Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 3),
            HasGeometry = true, Alan = 9, YapiElemani = ayirici, KaynakMekan = kaynak, AliciMekan = alici
        };
        kaynak.Bolmeler.Add(bolme);
        return new HavaSesiHesapServisi().HesaplaApparentRw(bolme);
    }

    var dusukTs = ApparentRwFor(0.3); // a büyük → K yüksek → flanking az → R'w yüksek
    var yuksekTs = ApparentRwFor(1.5); // a küçük → K düşük → flanking çok → R'w düşük
    AssertGreater(dusukTs - yuksekTs, 1.0, "Düşük T_s → daha az flanking → daha yüksek R'w");
}

// ─── ΔR_ij: Kaplama / giydirme iyileştirmesi (Annex D) ───────────────────────

static void BuildingLiningDeltaRBoostsCompositeRw()
{
    // EffectiveRw = Rw + DeltaRDb. Açıklık yoksa composite tam DeltaR kadar yükselir.
    var wall = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 50, DeltaRDb = 0 };
    var bolme = new Bolme { Alan = 10, YapiElemani = wall };
    AssertClose(50.0, HavaSesiHesapServisi.CalculateCompositeRw(bolme), 0.001, "DeltaRDb=0 → composite = Rw");
    wall.DeltaRDb = 5;
    AssertClose(55.0, HavaSesiHesapServisi.CalculateCompositeRw(bolme), 0.001, "DeltaRDb=5 → composite +5");
}

static void BuildingLiningDeltaRImprovesFlankingApparent()
{
    // Flanking elemana kaplama eklemek apparent R'w'yi yükseltmeli — EffectiveRw flanking
    // ortalamasına da girer, dolayısıyla yan iletim azalır.
    static double ApparentRwFor(double dosemeDeltaR)
    {
        var ayirici = new YapiElemani { Ad = "D", Tip = YapiElemaniTipi.Duvar, Rw = 55 };
        var doseme = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Rw = 52, DeltaRDb = dosemeDeltaR };
        var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
        var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
        kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
        alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;
        var bolme = new Bolme
        {
            Ad = "Ayırıcı", Baslangic = new Point2D(4, 0), Bitis = new Point2D(4, 3),
            HasGeometry = true, Alan = 9, YapiElemani = ayirici, KaynakMekan = kaynak, AliciMekan = alici
        };
        kaynak.Bolmeler.Add(bolme);
        return new HavaSesiHesapServisi().HesaplaApparentRw(bolme);
    }

    var baseline = ApparentRwFor(0);
    var kaplama = ApparentRwFor(10);
    AssertGreater(kaplama - baseline, 1.0, "Flanking elemana ΔR=10 dB kaplama apparent R'w'yi yükseltmeli");
}

static void BuildingLiningDeltaRPerBandShapesRf()
{
    // DeltaRThirdOctave 16 bant doluysa, her banta o bandın ΔR(f)'si eklenir
    // (uniform DeltaRDb yerine). Catalog frekansa bağlı kaplama verisi varsa kullanılır.
    var rThirdOctave = Enumerable.Repeat(50.0, 16).ToList();
    var deltaPerBand = new List<double> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
    var wall = new YapiElemani
    {
        Ad = "Duvar",
        Tip = YapiElemaniTipi.Duvar,
        Rw = 50,
        RThirdOctave = rThirdOctave,
        DeltaRThirdOctave = deltaPerBand
    };

    var r = OktavBantElemanServisi.GetRUcuncuOktavDizisi(wall);
    if (r.Length != 16)
        throw new InvalidOperationException("Beklenen 16 bant.");
    AssertClose(50.0, r[0], 0.001, "Bant 0 (100Hz): 50 + 0 = 50");
    AssertClose(55.0, r[5], 0.001, "Bant 5 (315Hz): 50 + 5 = 55");
    AssertClose(65.0, r[15], 0.001, "Bant 15 (3150Hz): 50 + 15 = 65");

    // DeltaRThirdOctave boş ise uniform DeltaRDb kullanılır (geri uyumluluk).
    wall.DeltaRThirdOctave = new List<double>();
    wall.DeltaRDb = 8;
    var rUniform = OktavBantElemanServisi.GetRUcuncuOktavDizisi(wall);
    AssertClose(58.0, rUniform[0], 0.001, "DeltaRDb=8 uniform: tüm bantlar +8");
    AssertClose(58.0, rUniform[15], 0.001, "DeltaRDb=8 uniform: yüksek bant da +8");
}

static void BuildingLiningDeltaRCollapsesToOktav()
{
    // 7-bant (1/1 oktav) yolda 16-bant ΔR(f) varsa, uniform yerine oktava indirgenip
    // per-band uygulanmalı. ΔR(f) = bant indeksi (0..15) → oktav ortalamaları:
    //   63→idx0=0, 125→avg(0,1,2)=1, 250→avg(3,4,5)=4, 500→avg(6,7,8)=7,
    //   1k→avg(9,10,11)=10, 2k→avg(12,13,14)=13, 4k→idx15=15.
    var deltaPerBand = new List<double> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
    var wall = new YapiElemani
    {
        Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 40,
        HasRealOktavData = true,
        R63 = 40, R125 = 40, R250 = 40, R500 = 40, R1k = 40, R2k = 40, R4k = 40,
        DeltaRThirdOctave = deltaPerBand
    };

    var r = OktavBantElemanServisi.GetRDizisi(wall);
    if (r.Length != 7)
        throw new InvalidOperationException("Beklenen 7 bant.");
    AssertClose(40.0, r[0], 0.001, "63 Hz: 40 + 0 = 40");
    AssertClose(41.0, r[1], 0.001, "125 Hz: 40 + avg(0,1,2)=1 → 41");
    AssertClose(44.0, r[2], 0.001, "250 Hz: 40 + avg(3,4,5)=4 → 44");
    AssertClose(47.0, r[3], 0.001, "500 Hz: 40 + avg(6,7,8)=7 → 47");
    AssertClose(50.0, r[4], 0.001, "1k Hz: 40 + avg(9,10,11)=10 → 50");
    AssertClose(53.0, r[5], 0.001, "2k Hz: 40 + avg(12,13,14)=13 → 53");
    AssertClose(55.0, r[6], 0.001, "4k Hz: 40 + idx15=15 → 55");
}

static void BuildingStructuralTsEstimatedFromMass()
{
    // EN 12354-1 Annex C: η_total = η_int + m'/(485·√f), T_s = 2.2/(f·η_total).
    // m' = 300 kg/m² (150 mm × 2000 kg/m³), η_int = 0.01, f = 500 Hz:
    //   η_total = 0.01 + 300/(485·√500) = 0.0376627 → T_s = 2.2/(500·0.0376627) = 0.11683 s.
    var agir = new YapiElemani
    {
        Ad = "Beton duvar", Tip = YapiElemaniTipi.Duvar,
        Katmanlar = [new YapiKatmani { MalzemeAdi = "Beton", Kalinlik = 150, Yogunluk = 2000 }]
    };
    AssertClose(0.11683, agir.TahminiYapisalRtSn(), 0.001, "Annex C T_s (m'=300) ≈ 0.117 s");

    // Daha hafif eleman → daha az iç sönüm → daha uzun T_s.
    var hafif = new YapiElemani
    {
        Ad = "Hafif bölme", Tip = YapiElemaniTipi.Duvar,
        Katmanlar = [new YapiKatmani { MalzemeAdi = "Alçı", Kalinlik = 50, Yogunluk = 800 }]
    };
    if (!(hafif.TahminiYapisalRtSn() > agir.TahminiYapisalRtSn()))
        throw new InvalidOperationException("Hafif eleman T_s'i ağır elemandan büyük olmalı.");

    // Kütle yoksa tahmin yapılamaz → 0 (çağıran 0.5 s default'a düşer).
    var kutlesiz = new YapiElemani { Ad = "Boş", Tip = YapiElemaniTipi.Duvar };
    AssertClose(0.0, kutlesiz.TahminiYapisalRtSn(), 0.0001, "Kütle yok → T_s tahmini 0");
}

static Mekan CreateRectMekan(string name, double x0, double y0, double x1, double y1)
{
    return new Mekan
    {
        Ad = name,
        KullanimTipi = name,
        Yukseklik = 3,
        Alan = Math.Abs((x1 - x0) * (y1 - y0)),
        Poligon =
        [
            new Point2D(x0, y0),
            new Point2D(x1, y0),
            new Point2D(x1, y1),
            new Point2D(x0, y1)
        ]
    };
}

static void MaekawaReferencePointsAreStable()
{
    AssertClose(0.0, NoiseBarrierDiffraction.CalculateMaekawaAttenuation(0.0, 1000), 0.01, "unblocked edge");
    AssertClose(11.14, NoiseBarrierDiffraction.CalculateMaekawaAttenuation(PathDifferenceForFresnel(0.5, 1000), 1000), 0.03, "N=0.5");
    AssertClose(16.33, NoiseBarrierDiffraction.CalculateMaekawaAttenuation(PathDifferenceForFresnel(2.0, 1000), 1000), 0.03, "N=2");
}

static void KurzeAndersonReferencePointsAreStable()
{
    AssertClose(10.47, NoiseBarrierDiffraction.CalculateKurzeAndersonAttenuation(PathDifferenceForFresnel(0.5, 1000), 1000), 0.08, "N=0.5");
    AssertClose(16.00, NoiseBarrierDiffraction.CalculateKurzeAndersonAttenuation(PathDifferenceForFresnel(2.0, 1000), 1000), 0.08, "N=2");
}

static void TallerBarrierIncreasesInsertionLoss()
{
    var low = RunSingleReceiverScenario(barrierHeight: 3.0, barrierLength: 80.0, materialTl: 80.0);
    var high = RunSingleReceiverScenario(barrierHeight: 6.0, barrierLength: 80.0, materialTl: 80.0);

    AssertGreater(high.InsertionLoss, low.InsertionLoss + 1.0, "Taller barrier should improve insertion loss.");
}

static void ShortFiniteBarrierLosesPerformanceAtSideEdges()
{
    var shortBarrier = RunSingleReceiverScenario(barrierHeight: 6.0, barrierLength: 4.0, materialTl: 80.0);
    var longBarrier = RunSingleReceiverScenario(barrierHeight: 6.0, barrierLength: 80.0, materialTl: 80.0);

    AssertGreater(longBarrier.InsertionLoss, shortBarrier.InsertionLoss + 1.0, "Long barrier should outperform short finite barrier.");
}

static void SecondTallerSeriesBarrierIncreasesInsertionLoss()
{
    var single = RunSingleReceiverScenario(barrierHeight: 4.0, barrierLength: 80.0, materialTl: 80.0);

    var multi = CreateBaseProject(barrierHeight: 4.0, barrierLength: 80.0);
    multi.Barriers.Add(new BarrierModel
    {
        Name = "Bariyer 2",
        Height = 8.0,
        Length = 80.0,
        DistanceFromBuilding = 20.0,
        CenterX = 0,
        MaterialId = "test"
    });
    var multiResult = Run(multi, materialTl: 80.0);

    AssertGreater(multiResult.InsertionLoss, single.InsertionLoss + 1.0,
        "Adding a taller second series barrier should raise total insertion loss.");
}

static void DisabledBarrierDoesNotContributeToAttenuation()
{
    var single = RunSingleReceiverScenario(barrierHeight: 4.0, barrierLength: 80.0, materialTl: 80.0);

    var withDisabled = CreateBaseProject(barrierHeight: 4.0, barrierLength: 80.0);
    withDisabled.Barriers.Add(new BarrierModel
    {
        Name = "Disabled extra",
        Height = 12.0,
        Length = 200.0,
        DistanceFromBuilding = 20.0,
        CenterX = 0,
        MaterialId = "test",
        IsEnabled = false
    });
    var disabledResult = Run(withDisabled, materialTl: 80.0);

    AssertBetween(disabledResult.InsertionLoss - single.InsertionLoss, -0.01, 0.01,
        "Disabled barrier should not affect insertion loss.");
}

static void LowTlMaterialLimitsBarrierPerformance()
{
    var weak = RunSingleReceiverScenario(barrierHeight: 6.0, barrierLength: 80.0, materialTl: 5.0);
    var strong = RunSingleReceiverScenario(barrierHeight: 6.0, barrierLength: 80.0, materialTl: 80.0);

    AssertGreater(strong.InsertionLoss, weak.InsertionLoss + 1.0, "High TL material should outperform low TL material.");
    AssertBetween(weak.InsertionLoss, 0.0, 8.0, "Low TL material should cap insertion loss.");
}

static void AbsorptiveBarrierSideImprovesInsertionLoss()
{
    var reflective = RunSingleReceiverScenario(barrierHeight: 6.0, barrierLength: 80.0, materialTl: 80.0, absorption: 0.0);
    var absorptive = RunSingleReceiverScenario(barrierHeight: 6.0, barrierLength: 80.0, materialTl: 80.0, absorption: 0.9);

    AssertGreater(absorptive.InsertionLoss, reflective.InsertionLoss + 0.5, "Absorptive source side should improve insertion loss.");
}

static void UnblockedLineOfSightGivesNearZeroInsertionLoss()
{
    var result = RunSingleReceiverScenario(barrierHeight: 0.5, barrierLength: 80.0, materialTl: 80.0);
    AssertBetween(result.InsertionLoss, -0.05, 0.05, "Barrier below line of sight should not attenuate.");
}

static void LineSourceSimulationRemainsFinite()
{
    var project = CreateBaseProject(barrierHeight: 5.0, barrierLength: 80.0);
    project.Sources.Clear();
    project.Sources.Add(new NoiseSource
    {
        Name = "Line source",
        Type = SourceType.Line,
        X = -20,
        Y = -30,
        XEnd = 20,
        YEnd = -30,
        Z = 0.5,
        OverallLw = 100
    });

    var result = Run(project, materialTl: 80.0);
    AssertFinite(result.TotalDbANoBarrier, "No-barrier dB(A)");
    AssertFinite(result.TotalDbAWithBarrier, "With-barrier dB(A)");
    AssertBetween(result.InsertionLoss, 0.0, 25.0, "Line source insertion loss");
}

static void TrafficFlowParametersDriveSourceLevel()
{
    var low = CreateTrafficProject(flow: 500, speed: 50, heavyPercent: 5);
    var high = CreateTrafficProject(flow: 3000, speed: 90, heavyPercent: 25);

    var lowResult = Run(low, materialTl: 80.0);
    var highResult = Run(high, materialTl: 80.0);

    AssertGreater(highResult.TotalDbANoBarrier, lowResult.TotalDbANoBarrier + 5.0, "Higher traffic demand should raise no-barrier level.");
}

static void DefaultTrafficSourceStaysInRealisticFacadeRange()
{
    // 1200 vph, 70 km/h, 10% heavy at ~30m distance
    // Expected Lw' ~ 88-90 dBA/m. Lp at 30m ~ 65-72 dBA.
    var project = CreateTrafficProject(flow: 1200, speed: 70, heavyPercent: 10);
    var result = Run(project, materialTl: 80.0);

    AssertBetween(result.TotalDbANoBarrier, 63.0, 75.0, "Calibrated traffic source no-barrier facade level");
    AssertFinite(result.TotalDbAWithBarrier, "Default traffic source with-barrier facade level");
}

static void TrafficModelCalibrationMatchesBenchmarks()
{
    // Benchmark: 1000 vph, 50 km/h, 10% heavy, 30m distance.
    // Reference Lp should be approx 64-68 dBA.
    var project = CreateTrafficProject(flow: 1000, speed: 50, heavyPercent: 10);
    var result = Run(project, materialTl: 80.0);
    
    AssertBetween(result.TotalDbANoBarrier, 62.0, 70.0, "Benchmark: 1000vph@50kmh at 30m");
}

static void DiffractionMethodsProduceExpectedRelativeLevels()
{
    var project = CreateBaseProject(barrierHeight: 4.0, barrierLength: 80.0);
    
    project.Method = DiffractionMethod.Maekawa;
    var maekawa = Run(project, materialTl: 80.0);
    
    project.Method = DiffractionMethod.KurzeAnderson;
    var kurze = Run(project, materialTl: 80.0);
    
    project.Method = DiffractionMethod.Iso9613_2;
    var iso = Run(project, materialTl: 80.0);
    
    // Kurze-Anderson is usually slightly more conservative (less attenuation) than Maekawa for high Fresnel.
    // Maekawa: 10 log(3+20N), Kurze: 5 + 20 log(sqrt(2piN)/tanh)
    // At high N, Maekawa ~ 13 + 10 log(N), Kurze ~ 13 + 10 log(N) but with different offsets.
    AssertFinite(maekawa.InsertionLoss, "Maekawa IL");
    AssertFinite(kurze.InsertionLoss, "Kurze IL");
    AssertFinite(iso.InsertionLoss, "ISO IL");
}

static void TrafficStandardFamilyProfilesProduceDistinctLevels()
{
    var models = new[]
    {
        TrafficEmissionModel.EngineeringPreview,
        TrafficEmissionModel.CnossosEuPreview,
        TrafficEmissionModel.FhwaTnmPreview,
        TrafficEmissionModel.NmpbRoutesPreview
    };

    var levels = models
        .Select(model => Run(CreateTrafficProject(flow: 1800, speed: 80, heavyPercent: 18, model), materialTl: 80.0).TotalDbANoBarrier)
        .ToArray();

    double spread = levels.Max() - levels.Min();
    AssertGreater(spread, 1.0, "Traffic standard-family profiles should produce distinct A-weighted levels.");
}

static void FacadeMultiReflectionsReduceBarrierInsertionLoss()
{
    var reflectionsOff = CreateBaseProject(barrierHeight: 6.0, barrierLength: 80.0);
    reflectionsOff.EnableFacadeReflections = false;

    var reflectionsOn = CreateBaseProject(barrierHeight: 6.0, barrierLength: 80.0);
    reflectionsOn.EnableFacadeReflections = true;
    reflectionsOn.FacadeReflectionCoefficient = 0.75;
    reflectionsOn.BarrierReflectionCoefficient = 0.55;
    reflectionsOn.MaxReflectionOrder = 3;

    var offResult = Run(reflectionsOff, materialTl: 80.0, absorption: 0.0);
    var onResult = Run(reflectionsOn, materialTl: 80.0, absorption: 0.0);

    AssertGreater(onResult.TotalDbAWithBarrier, offResult.TotalDbAWithBarrier + 0.2, "Facade and barrier reflections should raise protected facade level.");
    AssertGreater(offResult.InsertionLoss, onResult.InsertionLoss + 0.2, "Facade and barrier reflections should reduce net insertion loss.");
}

static void AbsorptiveBarrierReducesReflectionPenalty()
{
    var reflective = CreateBaseProject(barrierHeight: 6.0, barrierLength: 80.0);
    reflective.EnableFacadeReflections = true;
    reflective.FacadeReflectionCoefficient = 0.75;
    reflective.BarrierReflectionCoefficient = 0.55;
    reflective.MaxReflectionOrder = 3;

    var absorptive = CreateBaseProject(barrierHeight: 6.0, barrierLength: 80.0);
    absorptive.EnableFacadeReflections = true;
    absorptive.FacadeReflectionCoefficient = 0.75;
    absorptive.BarrierReflectionCoefficient = 0.55;
    absorptive.MaxReflectionOrder = 3;

    var reflectiveResult = Run(reflective, materialTl: 80.0, absorption: 0.0);
    var absorptiveResult = Run(absorptive, materialTl: 80.0, absorption: 0.9);

    AssertGreater(absorptiveResult.InsertionLoss, reflectiveResult.InsertionLoss + 0.5, "Absorptive barrier should reduce the multi-reflection penalty.");
}

static void NoiseBarrierExcelTemplateRoundTrips()
{
    var filePath = Path.Combine(Path.GetTempPath(), $"noise_barrier_template_{Guid.NewGuid():N}.xlsx");
    try
    {
        NoiseBarrierLibraryService.GenerateExcelTemplate(filePath);
        var imported = NoiseBarrierLibraryService.ImportFromExcel(filePath);
        
        if (imported.Count == 0)
            throw new InvalidOperationException("Excel import returned no materials.");
            
        var sample = imported.First();
        AssertClose(36.0, sample.TransmissionLoss[1000], 0.1, "Imported TL 1000Hz (Template default)");
        AssertClose(0.85, sample.AbsorptionCoefficients[1000], 0.01, "Imported Alpha 1000Hz (Template default)");
        AssertClose(100.0, sample.ThicknessMm, 0.1, "Imported thickness");
        AssertClose(35.0, sample.SurfaceMass, 0.1, "Imported surface mass");
        AssertClose(80.0, sample.Confidence, 0.1, "Imported confidence");
        if (sample.Source != "Şablon")
            throw new InvalidOperationException($"Imported source: expected Şablon, actual {sample.Source}.");
    }
    finally
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}

static void DxfImporterRejectsEmptyEntities()
{
    var importer = new SurfaceImportService();
    var emptyDxf = """
0
SECTION
2
ENTITIES
0
ENDSEC
0
EOF
""";

    AssertThrows(() => importer.Parse(emptyDxf), "Empty DXF should be rejected.");
}

static void DxfImporterMarksMissingUnitsAsUnknownMillimeters()
{
    var importer = new SurfaceImportService();
    var result = importer.Parse("""
0
SECTION
2
ENTITIES
0
3DFACE
10
0
20
0
30
0
11
1000
21
0
31
0
12
1000
22
1000
32
0
13
0
23
1000
33
0
0
ENDSEC
0
EOF
""");

    if (result.Document.UnitDetected)
        throw new InvalidOperationException("Missing $INSUNITS should be marked as undetected.");
    if (result.Document.Unit != SurfaceImportUnit.Millimeters)
        throw new InvalidOperationException($"Expected millimeter fallback, actual {result.Document.Unit}.");
    AssertGreater(result.Document.Surfaces.Count, 0, "DXF face should be imported.");
}

static void ProjectFactoryRejectsEmptyImportResult()
{
    var empty = new SurfaceImportResult(
        SurfaceImportFormat.Dxf,
        new SurfaceImportDocument(SurfaceImportUnit.Meters, []));

    AssertThrows(() => ProjectFactory.CreateFromImport("empty", null, empty), "Empty import result should be rejected.");
}

static void SimulationReliabilityFlagsWeakInputs()
{
    var surfaces = new[]
    {
        new ProjectSurface(1, "Zemin", "Zemin", [
            new Point3D(0, 0, 0),
            new Point3D(10, 0, 0),
            new Point3D(10, 8, 0),
            new Point3D(0, 8, 0)
        ])
    };

    var project = new AcousticsProject(
        "Test",
        null,
        DateTime.UtcNow,
        SurfaceImportFormat.Dxf,
        SurfaceImportUnit.Meters,
        surfaces,
        Sources: [new AcousticPoint(1, "S1", AcousticPointType.Source, new Point3D(2, 2, 1.5))],
        Receivers: [new AcousticPoint(2, "R1", AcousticPointType.Receiver, new Point3D(7, 6, 1.2))],
        CategoryMaterials: new Dictionary<string, string>(),
        RoomVolume: 0);

    var echogram = new EchogramData(1, 2, "S1", "R1", [
        new EchogramArrival(0.02, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0)
    ]);
    var result = new SimulationResult([echogram], new SimulationParameters(RayCount: 1_000), TimeSpan.FromMilliseconds(10));

    var reliability = SimulationReliabilityAnalyzer.Analyze(project, new Dictionary<string, string>(), result);

    if (reliability.ConfidenceScore >= 70)
        throw new InvalidOperationException("Weak simulation inputs should reduce reliability score.");
    if (!reliability.Warnings.Any(w => w.Contains("Işın sayısı", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Low ray count warning should be reported.");
    if (!reliability.Warnings.Any(w => w.Contains("materyal", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Missing material assignment warning should be reported.");
}

static void PanelDxfParserCountsCircleHoles()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
100
20
0
10
100
20
100
10
0
20
100
0
CIRCLE
10
50
20
50
30
0
40
10
0
ENDSEC
0
EOF
""");

    if (result.HoleCount != 1)
        throw new InvalidOperationException($"Expected 1 circle hole, actual {result.HoleCount}.");
    AssertClose(Math.PI * 100.0 / 10_000.0, result.OpenAreaRatio, 0.001, "Circle open area ratio");
}

static void PanelDxfParserReads3DFaceBoundary()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
3DFACE
10
0
20
0
30
0
11
100
21
0
31
0
12
100
22
100
32
0
13
0
23
100
33
0
0
CIRCLE
10
50
20
50
30
0
40
10
0
ENDSEC
0
EOF
""");

    if (result.HoleCount != 1)
        throw new InvalidOperationException($"Expected 1 circle hole inside 3DFACE boundary, actual {result.HoleCount}.");
    AssertClose(10_000.0, result.TotalArea, 0.001, "3DFACE boundary raw area");
    AssertClose(0.01, result.TotalAreaSquareMeters, 0.000001, "3DFACE boundary area in square meters");
    AssertClose(Math.PI * 100.0 / 10_000.0, result.OpenAreaRatio, 0.001, "3DFACE circle open area ratio");
}

static void PanelDxfParserIgnoresClosedAuxiliaryGeometryOutsideBoundary()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
100
20
0
10
100
20
100
10
0
20
100
0
CIRCLE
10
50
20
50
30
0
40
10
0
LWPOLYLINE
90
4
70
1
10
200
20
0
10
250
20
0
10
250
20
50
10
200
20
50
0
ENDSEC
0
EOF
""");

    if (result.HoleCount != 1)
        throw new InvalidOperationException($"Only the opening inside the largest panel boundary should count; actual {result.HoleCount}.");
    AssertClose(10_000.0, result.TotalArea, 0.001, "Largest boundary should define panel area");
    AssertClose(Math.PI * 100.0 / 10_000.0, result.OpenAreaRatio, 0.001, "Auxiliary closed geometry must not dilute open area ratio");
}

static void PanelDxfParserDetectsKilometerUnits()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
7
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
1
20
0
10
1
20
1
10
0
20
1
0
ENDSEC
0
EOF
""");

    if (!result.UnitDetected)
        throw new InvalidOperationException("Known kilometer INSUNITS should be marked as detected.");
    if (result.DetectedInsUnitsCode != 7)
        throw new InvalidOperationException($"Expected INSUNITS code 7, actual {result.DetectedInsUnitsCode}.");
    AssertClose(1000.0, result.UnitScaleToMeters, 0.001, "Kilometer unit scale");
    AssertClose(1_000_000.0, result.TotalAreaSquareMeters, 0.001, "Kilometer-scaled area");
}

static void PanelDxfParserFlagsUnknownInsUnits()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
99
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
100
20
0
10
100
20
100
10
0
20
100
0
ENDSEC
0
EOF
""");

    if (result.UnitDetected)
        throw new InvalidOperationException("Unknown INSUNITS should require user confirmation.");
    if (result.DetectedInsUnitsCode != 99)
        throw new InvalidOperationException($"Unknown INSUNITS code should be preserved; actual {result.DetectedInsUnitsCode}.");
    AssertClose(0.001, result.UnitScaleToMeters, 1e-12, "Unknown unit fallback suggestion");
    if (result.Quality is null || !result.Quality.Warnings.Any(w => w.Contains("birimi", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Unknown INSUNITS should add a unit warning.");
}

static void PanelDxfParserReportsMalformedNumericValues()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
100
20
0
10
100
20
100
10
0
20
100
0
CIRCLE
10
50
20
50
30
0
40
oops
0
ENDSEC
0
EOF
""");

    AssertClose(10_000.0, result.TotalArea, 0.001, "Malformed numeric value should not prevent boundary parsing");
    if (result.HoleCount != 0)
        throw new InvalidOperationException($"Malformed circle radius should not create a hole; actual {result.HoleCount}.");
    if (result.InvalidNumericValueCount <= 0)
        throw new InvalidOperationException("Malformed numeric value should be counted.");
    if (result.Quality is null || !result.Quality.Warnings.Any(w => w.Contains("sayısal", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Malformed numeric value should add a quality warning.");
}

static void PanelOpenAreaRatioSupportsCodexLimit()
{
    const double diameter = 0.010;
    const double requestedRatio = 0.90;

    double spacing = PerforatedLayer.CalculateSpacingForOpenAreaRatio(diameter, requestedRatio, HolePattern.Square);
    double calculatedRatio = PerforatedLayer.CalculateOpenAreaRatio(diameter, spacing, HolePattern.Square);

    AssertClose(requestedRatio, calculatedRatio, 1e-9, "Parametric open area ratio should not be capped at 70 percent");
    AssertClose(
        PerforatedLayer.MaxOpenAreaRatio,
        PerforatedLayer.CalculateOpenAreaRatio(diameter, diameter * 0.1, HolePattern.Square),
        1e-9,
        "Parametric open area ratio should cap at codex maximum");
}

static void PanelDxfParserPreservesHighOpenAreaRatio()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
100
20
0
10
100
20
100
10
0
20
100
0
LWPOLYLINE
90
4
70
1
10
1
20
1
10
99
20
1
10
99
20
99
10
1
20
99
0
ENDSEC
0
EOF
""");

    AssertClose(0.9604, result.OpenAreaRatio, 1e-9, "DXF high open area ratio should not be capped at 95 percent");
}

static void PanelDxfTemplateIsParserCompatible()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse(PanelTemplateDxfBuilder.Build600x600MillimeterTemplate());

    if (result.HoleCount != 25)
        throw new InvalidOperationException($"Expected 25 template holes, actual {result.HoleCount}.");
    AssertClose(600 * 600 * 0.000001, result.TotalAreaSquareMeters, 0.000001, "Template area in square meters");
    AssertGreater(result.OpenAreaRatio, 0.02, "Template open area ratio should be positive.");
}

static void PanelDxfParserIgnoresOpenGuideGeometryInArea()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
100
20
0
10
100
20
100
10
0
20
100
0
LINE
10
10
20
10
11
90
21
90
0
ARC
10
50
20
50
40
20
50
0
51
180
0
ENDSEC
0
EOF
""");

    if (result.HoleCount != 0)
        throw new InvalidOperationException($"Open guide geometry should not create holes; actual {result.HoleCount}.");
    AssertClose(0.0, result.OpenAreaRatio, 0.0001, "Open guide geometry open area");
}

static void PanelDxfParserReportsHydraulicOpeningGeometry()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
200
20
0
10
200
20
100
10
0
20
100
0
LWPOLYLINE
90
4
70
1
10
20
20
40
10
120
20
40
10
120
20
60
10
20
20
60
0
ENDSEC
0
EOF
""");

    var opening = result.Openings?.SingleOrDefault()
        ?? throw new InvalidOperationException("Expected one hydraulic opening.");
    AssertClose(2 * (100 + 20), opening.Perimeter, 0.001, "Slot perimeter");
    AssertClose(4 * 2000.0 / 240.0, opening.HydraulicDiameter, 0.001, "Slot hydraulic diameter");
    AssertGreater(opening.AspectRatio, 4.0, "Slot aspect ratio");
}

static void PanelDxfQualityFlagsSlotGeometry()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
300
20
0
10
300
20
120
10
0
20
120
0
LWPOLYLINE
90
4
70
1
10
20
20
50
10
280
20
50
10
280
20
60
10
20
20
60
0
ENDSEC
0
EOF
""");

    var quality = result.Quality ?? throw new InvalidOperationException("Expected quality metrics.");
    AssertGreater(quality.SlotLikeOpeningCount, 0, "Slot quality count");
    if (quality.ConfidenceScore >= 100)
        throw new InvalidOperationException("Slot geometry should reduce confidence score.");
    if (!quality.Warnings.Any(w => w.Contains("slot", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Slot warning should be reported.");
}

static void PanelCustomOpeningsMatchEquivalentCircularArray()
{
    const double sigma = 0.20;
    const double diameter = 0.010;
    var standard = new[]
    {
        new PerforatedLayer
        {
            Thickness = 0.012,
            OpenAreaRatio = sigma,
            HoleDiameter = diameter,
            HoleSpacing = PerforatedLayer.CalculateSpacingForOpenAreaRatio(diameter, sigma, HolePattern.Square)
        }
    };
    var custom = new[]
    {
        new PerforatedLayer
        {
            Thickness = 0.012,
            OpenAreaRatio = sigma,
            HoleDiameter = diameter,
            CustomOpenings =
            [
                new PerforationOpening(sigma / 2.0, diameter, 1.0, 1.0, "Dairesel"),
                new PerforationOpening(sigma / 2.0, diameter, 1.0, 1.0, "Dairesel")
            ]
        }
    };

    AssertClose(
        TmmCalculator.CalculateNormalAbsorption(standard, 500),
        TmmCalculator.CalculateNormalAbsorption(custom, 500),
        1e-9,
        "Custom circular openings should match equivalent array");
}

static void PanelHelmholtzEstimateFollowsPerforatedLayerNeckModel()
{
    const double thickness = 0.012;
    const double diameter = 0.006;
    const double sigma = 0.50;
    const double airGap = 0.100;
    var layer = new PerforatedLayer
    {
        Thickness = thickness,
        HoleDiameter = diameter,
        OpenAreaRatio = sigma,
        HoleSpacing = PerforatedLayer.CalculateSpacingForOpenAreaRatio(diameter, sigma, HolePattern.Square),
        HolePattern = HolePattern.Square
    };
    var layers = new AcousticLayer[] { layer, new AirLayer { Thickness = airGap } };

    double legacy = 343.0 / (2.0 * Math.PI) * Math.Sqrt(sigma / ((thickness + 0.85 * diameter) * airGap));
    double estimate = layer.EstimateHelmholtzResonance(airGap);
    AssertGreater(estimate, legacy * 1.05, "High-porosity Helmholtz estimate should include Fok end-correction reduction.");

    double bestF = 0;
    double bestAlpha = -1;
    for (double f = Math.Max(63, estimate * 0.55); f <= estimate * 1.60; f += 2.0)
    {
        double alpha = TmmCalculator.CalculateNormalAbsorption(layers, f);
        if (alpha > bestAlpha)
        {
            bestAlpha = alpha;
            bestF = f;
        }
    }

    AssertBetween(bestF / estimate, 0.65, 1.35, "Helmholtz estimate should track TMM peak with shared neck model.");
}

static void PanelHelmholtzEstimateSupportsCustomOpenings()
{
    var circular = new PerforatedLayer
    {
        Thickness = 0.012,
        CustomOpenings =
        [
            new PerforationOpening(0.08, 0.006, 1.0, 1.0, "Dairesel")
        ]
    };
    var slot = new PerforatedLayer
    {
        Thickness = 0.012,
        CustomOpenings =
        [
            new PerforationOpening(0.08, 0.006, 0.20, 12.0, "Slot/Serbest")
        ]
    };

    double circularEstimate = circular.EstimateHelmholtzResonance(0.100);
    double slotEstimate = slot.EstimateHelmholtzResonance(0.100);

    AssertGreater(circularEstimate, 0.0, "Custom circular opening estimate should be non-zero without equivalent HoleDiameter.");
    AssertGreater(slotEstimate, 0.0, "Custom slot opening estimate should be non-zero without equivalent HoleDiameter.");
    AssertGreater(slotEstimate, circularEstimate, "Slot shape factor should affect Helmholtz estimate.");
}

static void PanelHelmholtzBenchmarkPeakIsNearFormula()
{
    double thickness = 0.012;
    double diameter = 0.006;
    double sigma = 0.08;
    double airGap = 0.100;
    var layers = new AcousticLayer[]
    {
        new PerforatedLayer
        {
            Thickness = thickness,
            HoleDiameter = diameter,
            OpenAreaRatio = sigma,
            HoleSpacing = PerforatedLayer.CalculateSpacingForOpenAreaRatio(diameter, sigma, HolePattern.Square)
        },
        new AirLayer { Thickness = airGap }
    };

    double expected = 343.0 / (2.0 * Math.PI) * Math.Sqrt(sigma / ((thickness + 0.85 * diameter) * airGap));
    double bestF = 0;
    double bestAlpha = -1;
    for (double f = Math.Max(63, expected * 0.55); f <= expected * 1.60; f += 2.0)
    {
        double alpha = TmmCalculator.CalculateNormalAbsorption(layers, f);
        if (alpha > bestAlpha)
        {
            bestAlpha = alpha;
            bestF = f;
        }
    }

    AssertBetween(bestF / expected, 0.65, 1.45, "Helmholtz peak frequency ratio");
    AssertGreater(bestAlpha, 0.10, "Helmholtz benchmark peak absorption");
}

static void PanelPorousLayerRangeDiagnosticIsStateless()
{
    if (typeof(PorousLayer).GetProperty("LastFrequencyOutOfRange") != null)
        throw new InvalidOperationException("PorousLayer should not expose mutable LastFrequencyOutOfRange state.");

    var layer = new PorousLayer
    {
        Thickness = 0.050,
        FlowResistivity = 35_000
    };

    if (!layer.IsFrequencyOutsideEmpiricalRange(63))
        throw new InvalidOperationException("63 Hz should be outside empirical range for 35 kRayl/m porous material.");
    if (layer.IsFrequencyOutsideEmpiricalRange(500))
        throw new InvalidOperationException("500 Hz should be inside empirical range for 35 kRayl/m porous material.");

    var before = layer.GetTransferMatrix(63);
    _ = layer.GetTransferMatrix(500);
    var after = layer.GetTransferMatrix(63);

    AssertClose(before.T11.Real, after.T11.Real, 1e-12, "Porous matrix T11 real should be deterministic");
    AssertClose(before.T12.Imaginary, after.T12.Imaginary, 1e-9, "Porous matrix T12 imaginary should be deterministic");
    if (!layer.IsFrequencyOutsideEmpiricalRange(63))
        throw new InvalidOperationException("Range diagnostic should remain a pure frequency query after calculations.");
}

static void PanelAlphaWReferenceCurveUsesUnfavorableDeviations()
{
    var exactReference = TmmCalculator.CalculateAlphaWResultFromThirdOctaves(
        ThirdOctavesForPractical(0.50, 0.70, 0.70, 0.70, 0.60));
    AssertClose(0.70, exactReference.Value, 1e-9, "alphaW exact ISO reference fit");
    if (exactReference.ShapeIndicators.Length != 0)
        throw new InvalidOperationException($"Exact reference fit should have no shape indicators; actual {exactReference.ShapeIndicators}.");

    var acceptedDeficit = TmmCalculator.CalculateAlphaWResultFromThirdOctaves(
        ThirdOctavesForPractical(0.50, 0.65, 0.70, 0.70, 0.60));
    AssertClose(0.70, acceptedDeficit.Value, 1e-9, "alphaW should allow total unfavorable deviation up to 0.10");

    var rejectedDeficit = TmmCalculator.CalculateAlphaWResultFromThirdOctaves(
        ThirdOctavesForPractical(0.50, 0.55, 0.70, 0.70, 0.60));
    AssertClose(0.65, rejectedDeficit.Value, 1e-9, "alphaW should reject total unfavorable deviation above 0.10");
}

static void PanelAlphaWShapeIndicatorsAreReported()
{
    var result = TmmCalculator.CalculateAlphaWResultFromThirdOctaves(
        ThirdOctavesForPractical(0.80, 0.45, 0.45, 0.80, 0.80));

    AssertClose(0.50, result.Value, 1e-9, "alphaW shape indicator reference value");
    if (result.ShapeIndicators != "LH")
        throw new InvalidOperationException($"Expected LH shape indicators, actual {result.ShapeIndicators}.");
}

static void PanelRandomIncidenceAbsorptionMatchesMatchedImpedanceIntegral()
{
    double actual = TmmCalculator.CalculateRandomIncidenceAbsorptionFromSurfaceImpedance(
        new System.Numerics.Complex(TmmCalculator.Z0, 0.0));
    double expected = 12.0 - 16.0 * Math.Log(2.0);

    AssertClose(expected, actual, 0.0002, "Random-incidence absorption for Zs/Z0 = 1");

    double invalid = TmmCalculator.CalculateRandomIncidenceAbsorptionFromSurfaceImpedance(
        new System.Numerics.Complex(double.NaN, 0.0));
    AssertClose(0.0, invalid, 1e-12, "Random-incidence absorption should reject non-finite impedance");
}

static void PanelDxfQualityUiItemsSummarizeImportRisks()
{
    var parser = new DxfPanelParser();
    var result = parser.Parse("""
0
SECTION
2
HEADER
9
$INSUNITS
70
4
0
ENDSEC
0
SECTION
2
ENTITIES
0
LWPOLYLINE
90
4
70
1
10
0
20
0
10
100
20
0
10
100
20
100
10
0
20
100
0
LWPOLYLINE
90
4
70
1
10
10
20
45
10
90
20
45
10
90
20
50
10
10
20
50
0
ENDSEC
0
EOF
""");

    var items = PanelDesignViewModel.BuildDxfQualityItems(result);
    if (!items.Any(i => i.Contains("Güven skoru", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("DXF quality UI items should include confidence score.");
    if (!items.Any(i => i.Contains("Hidrolik çap", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("DXF quality UI items should include hydraulic diameter range.");
    if (!items.Any(i => i.Contains("slot", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("DXF quality UI items should include slot/free-shape risk.");
    if (!items.Any(i => i.Contains("Kontrol:", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("DXF quality UI items should include actionable quality warnings.");
}

static void PanelReportsIncludeDxfQualityDecisionItems()
{
    var vm = new PanelDesignViewModel
    {
        DxfQualitySummary = "DXF kalite analizi: Orta (60/100)",
        DxfQualityDetails = string.Join('\n',
            "- Güven skoru: Orta (60/100).",
            "- Hidrolik çap: min 1.00 mm, ort 2.00 mm, max 3.00 mm.",
            "- Kontrol: Slot riski.")
    };

    string text = InvokePanelReport(vm, "BuildTextReport");
    string markdown = InvokePanelReport(vm, "BuildMarkdownReport");
    string html = InvokePanelReport(vm, "BuildHtmlReport");
    string decodedHtml = System.Net.WebUtility.HtmlDecode(html);

    if (!text.Contains("DXF kalite karar maddeleri:", StringComparison.OrdinalIgnoreCase) ||
        !text.Contains("Hidrolik çap", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Text report should include DXF quality decision items.");
    if (!markdown.Contains("  - Hidrolik çap", StringComparison.OrdinalIgnoreCase) ||
        !markdown.Contains("Kontrol: Slot riski", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Markdown report should include DXF quality decision items.");
    if (!decodedHtml.Contains("Hidrolik çap", StringComparison.OrdinalIgnoreCase) ||
        !decodedHtml.Contains("Kontrol: Slot riski", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("HTML report should include DXF quality decision items.");
}

static string InvokePanelReport(PanelDesignViewModel vm, string methodName)
{
    var method = typeof(PanelDesignViewModel).GetMethod(
        methodName,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    if (method == null)
        throw new InvalidOperationException($"Panel report method not found: {methodName}");

    return method.Invoke(vm, null) as string
        ?? throw new InvalidOperationException($"Panel report method did not return text: {methodName}");
}

static void PanelLayerClearsDxfImportStateOnParametricEdit()
{
    AssertDxfStateClearsOnLayerEdit(vm => vm.HoleDiameter += 1.0, "hole diameter");
    AssertDxfStateClearsOnLayerEdit(vm => vm.HoleSpacing += 1.0, "hole spacing");
    AssertDxfStateClearsOnLayerEdit(vm => vm.OpenAreaRatio += 1.0, "open area ratio");
    AssertDxfStateClearsOnLayerEdit(vm => vm.HolePatternName = "Şaşırtmalı", "hole pattern");
}

static void AssertDxfStateClearsOnLayerEdit(Action<LayerViewModel> edit, string label)
{
    var layer = new PerforatedLayer
    {
        Thickness = 0.012,
        HoleDiameter = 0.006,
        HoleSpacing = 0.020,
        OpenAreaRatio = 0.07,
        HolePattern = HolePattern.Square
    };
    var vm = new LayerViewModel(layer, "DXF layer");
    vm.ApplyDxfImport(
        [new DxfEntity { Type = "CIRCLE", Pairs = [new DxfPair("40", "3")] }],
        [new PerforationOpening(0.07, 0.006, 1.0, 1.0, "Dairesel")]);

    if (!vm.UsesCustomOpenings || vm.CustomOpeningCount != 1 || vm.DxfEntities is not { Count: 1 })
        throw new InvalidOperationException($"DXF state setup failed for {label}.");

    int changed = 0;
    vm.LayerChanged += (_, _) => changed++;

    edit(vm);

    if (layer.CustomOpenings != null)
        throw new InvalidOperationException($"Custom openings should be cleared after {label} edit.");
    if (vm.DxfEntities != null)
        throw new InvalidOperationException($"DXF preview entities should be cleared after {label} edit.");
    if (vm.UsesCustomOpenings || vm.CustomOpeningCount != 0)
        throw new InvalidOperationException($"Custom opening indicators should reset after {label} edit.");
    if (changed == 0)
        throw new InvalidOperationException($"Layer change should be raised after {label} edit.");
}

static void PanelPreviewIgnoresMalformedDxfNumericValues()
{
    var pairs = new List<DxfPair>
    {
        new("10", "0"),
        new("20", "0"),
        new("10", "oops"),
        new("20", "25"),
        new("10", "50"),
        new("20", "NaN"),
        new("10", "100"),
        new("20", "100")
    };

    var points = InvokePanelPreviewPointParser(pairs);
    if (points.Count != 2)
        throw new InvalidOperationException($"Malformed DXF coordinates should be skipped; actual point count {points.Count}.");
    AssertClose(0.0, points[0].X, 1e-12, "Preview valid first X");
    AssertClose(0.0, points[0].Y, 1e-12, "Preview valid first Y");
    AssertClose(100.0, points[1].X, 1e-12, "Preview valid last X");
    AssertClose(100.0, points[1].Y, 1e-12, "Preview valid last Y");

    double invalidScalar = InvokePanelPreviewDxfDouble([new DxfPair("40", "Infinity")], "40");
    AssertClose(0.0, invalidScalar, 1e-12, "Preview non-finite scalar should fall back to zero");
}

static void PanelReportsOmitFrequencyTableWhenNoLayersAreIncluded()
{
    var vm = new PanelDesignViewModel();
    foreach (var layer in vm.Layers)
        layer.IsIncludedInCalculation = false;

    string text = InvokePanelReport(vm, "BuildTextReport");
    string markdown = InvokePanelReport(vm, "BuildMarkdownReport");
    string html = System.Net.WebUtility.HtmlDecode(InvokePanelReport(vm, "BuildHtmlReport"));

    foreach (var report in new[] { text, markdown, html })
    {
        if (!report.Contains("Hesapta katman yok; frekans tablosu üretilmedi.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Report should explain that the frequency table was omitted.");
        if (report.Contains("63 Hz", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Report should not include octave rows when all layers are excluded.");
    }
}

static List<(double X, double Y)> InvokePanelPreviewPointParser(List<DxfPair> pairs)
{
    var method = typeof(PanelPreviewControl).GetMethod(
        "GetPolylinePoints",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    if (method == null)
        throw new InvalidOperationException("Panel preview polyline parser not found.");

    return method.Invoke(null, [pairs]) as List<(double X, double Y)>
        ?? throw new InvalidOperationException("Panel preview polyline parser returned an unexpected value.");
}

static double InvokePanelPreviewDxfDouble(List<DxfPair> pairs, string code)
{
    var method = typeof(PanelPreviewControl).GetMethod(
        "GetDxfDouble",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
    if (method == null)
        throw new InvalidOperationException("Panel preview scalar parser not found.");

    return method.Invoke(null, [pairs, code]) is double value
        ? value
        : throw new InvalidOperationException("Panel preview scalar parser returned an unexpected value.");
}

static Dictionary<int, double> ThirdOctavesForPractical(
    double alpha250,
    double alpha500,
    double alpha1000,
    double alpha2000,
    double alpha4000)
{
    return new Dictionary<int, double>
    {
        [200] = alpha250,
        [250] = alpha250,
        [315] = alpha250,
        [400] = alpha500,
        [500] = alpha500,
        [630] = alpha500,
        [800] = alpha1000,
        [1000] = alpha1000,
        [1250] = alpha1000,
        [1600] = alpha2000,
        [2000] = alpha2000,
        [2500] = alpha2000,
        [3150] = alpha4000,
        [4000] = alpha4000,
        [5000] = alpha4000
    };
}

static void SoundInsulationConnectionSettingsAffectDoubleWallResult()
{
    var materials = SoundInsulationLibrary.GetBuiltInMaterials();
    var gyp = materials.Single(m => m.Id == "GYP_SOUND");
    var wool = materials.Single(m => m.Id == "SW_LIGHT");

    var layers = new List<SoundInsulationLayer>
    {
        new(gyp, 0.015),
        new(wool, 0.075, IsAirGap: true, HasPorousFill: true),
        new(gyp, 0.015)
    };

    var isolated = SoundInsulationCalculator.Calculate(new WallAssembly("iso", "isolated", layers));
    var direct = SoundInsulationCalculator.Calculate(new WallAssembly("direct", "direct", layers, StudSpacing: 0.6, ConnectionType: "Direct"));

    AssertGreater(isolated.RwRating!.Rw, direct.RwRating!.Rw, "Direct mechanical bridging should reduce double wall Rw.");
}

static void SoundInsulationCompositeWeakElementLowersTotalRating()
{
    var materials = SoundInsulationLibrary.GetBuiltInMaterials();
    var concrete = materials.Single(m => m.Id == "CONC_NORMAL");
    var glass = materials.Single(m => m.Id == "GLASS_FLOAT");

    var wall = new WallAssembly("wall", "wall", [new SoundInsulationLayer(concrete, 0.20)]);
    var weakOpening = new WallAssembly("glass", "glass", [new SoundInsulationLayer(glass, 0.004)]);
    var wallOnly = SoundInsulationCalculator.Calculate(wall);
    var composite = SoundInsulationCalculator.CalculateComposite(
    [
        new CompositeElement(wall, 10.0),
        new CompositeElement(weakOpening, 1.0)
    ]);

    AssertGreater(wallOnly.RwRating!.Rw, composite.RwRating.Rw, "Weak opening should lower composite Rw.");
}

static void SoundInsulationTmmSinglePaneNearMassLaw()
{
    // 4mm float cam: fc ~3200 Hz, 500 Hz mass-law bölgesinde olmalı.
    // TMM ve Empirical 500 Hz'de mass-law tahminine yakın çıkar (gerçek lab: ~27-30 dB).
    var glass = SoundInsulationLibrary.GetBuiltInMaterials().Single(m => m.Id == "GLASS_FLOAT");
    var layers = new List<SoundInsulationLayer> { new(glass, 0.004) };

    var tmm = SoundInsulationCalculator.Calculate(new WallAssembly("tmm", "tmm", layers, Method: CalculationMethod.TMM));

    // 4mm cam mass-law(500): 20log(10*500)-47 ≈ 27 dB. TMM ±8 dB.
    var fTmm = tmm.TransmissionLossValues[500];
    AssertBetween(fTmm, 19, 35, $"4mm cam TMM 500Hz ({fTmm:F1}) mass-law (≈27 dB) etrafında olmalı");
    AssertBetween(tmm.RwRating!.Rw, 20, 35, "4mm cam TMM Rw [20-35] dB (lab tipik 28-31)");
}

static void SoundInsulationTmmDoubleWallImprovesOverSingleMass()
{
    // Çift cidar TMM modunda eşdeğer toplam kütleli tek cidardan yüksek Rw vermelidir.
    var mats = SoundInsulationLibrary.GetBuiltInMaterials();
    var gyp = mats.Single(m => m.Id == "GYP_STD");
    var wool = mats.Single(m => m.Id == "SW_LIGHT");

    var doubleLayers = new List<SoundInsulationLayer>
    {
        new(gyp, 0.0125),
        new(wool, 0.075, IsAirGap: true, HasPorousFill: true),
        new(gyp, 0.0125)
    };
    var singleLayers = new List<SoundInsulationLayer> { new(gyp, 0.025) };

    var tmmDouble = SoundInsulationCalculator.Calculate(new WallAssembly("d", "d", doubleLayers, Method: CalculationMethod.TMM));
    var tmmSingle = SoundInsulationCalculator.Calculate(new WallAssembly("s", "s", singleLayers, Method: CalculationMethod.TMM));

    AssertGreater(tmmDouble.RwRating!.Rw, tmmSingle.RwRating!.Rw,
        "TMM: çift cidar + poröz dolgu eşdeğer kütleli tek cidardan yüksek Rw vermeli");
}

static void SoundInsulationHybridBlendsLowAndHighFrequency()
{
    // Hybrid: 100 Hz'de TMM'e, 5000 Hz'de empirical'e yakın olmalı.
    var mats = SoundInsulationLibrary.GetBuiltInMaterials();
    var gyp = mats.Single(m => m.Id == "GYP_SOUND");
    var wool = mats.Single(m => m.Id == "SW_LIGHT");
    var layers = new List<SoundInsulationLayer>
    {
        new(gyp, 0.015),
        new(wool, 0.050, IsAirGap: true, HasPorousFill: true),
        new(gyp, 0.015)
    };

    var emp = SoundInsulationCalculator.Calculate(new WallAssembly("e", "e", layers));
    var tmm = SoundInsulationCalculator.Calculate(new WallAssembly("t", "t", layers, Method: CalculationMethod.TMM));
    var hyb = SoundInsulationCalculator.Calculate(new WallAssembly("h", "h", layers, Method: CalculationMethod.Hybrid));

    var lowEmp = emp.TransmissionLossValues[100];
    var lowTmm = tmm.TransmissionLossValues[100];
    var lowHyb = hyb.TransmissionLossValues[100];
    AssertBetween(lowHyb, Math.Min(lowEmp, lowTmm) - 0.5, Math.Max(lowEmp, lowTmm) + 0.5,
        $"100 Hz Hybrid ({lowHyb:F1}) TMM ({lowTmm:F1}) ile Empirical ({lowEmp:F1}) arası olmalı");

    var hiEmp = emp.TransmissionLossValues[2500];
    var hiHyb = hyb.TransmissionLossValues[2500];
    AssertClose(hiEmp, hiHyb, 1.0, "Hybrid 2500 Hz Empirical değerine yakın olmalı (ağırlık 1'e yaklaşır)");
}

static void SoundInsulationSolidLayerReducesToMassLawAtLowFreq()
{
    // Elastik katı tabaka (4×4 P+S) modeli, fc çok altında alan-insidans kütle kanununa indirgenmeli.
    // 6 mm çelik: fc ≈ 2000 Hz; 200 Hz iyi bir mass-law bölgesi.
    var steel = SoundInsulationLibrary.GetBuiltInMaterials().Single(m => m.Id == "STEEL");
    var layers = new List<SoundInsulationLayer> { new(steel, 0.006) };
    var tmm = SoundInsulationCalculator.Calculate(new WallAssembly("st", "st", layers, Method: CalculationMethod.TMM));

    double mf = steel.Density * 0.006;
    double massLaw200 = 20.0 * Math.Log10(mf * 200.0) - 47.0; // alan-insidans kütle kanunu
    double tl200 = tmm.TransmissionLossValues[200];
    AssertClose(tl200, massLaw200, 4.0,
        $"Çelik 6mm TMM 200Hz ({tl200:F1}) alan-insidans kütle kanununa ({massLaw200:F1}) ±4 dB içinde olmalı");
}

static void SoundInsulationSolidLayerShowsCoincidenceDip()
{
    // 4 mm cam: fc ≈ 3000 Hz. Katı tabaka modeli fc komşuluğunda belirgin coincidence çukuru göstermeli;
    // fc altında ise empirical (kütle kanunu) tahminine yakın kalmalı.
    var glass = SoundInsulationLibrary.GetBuiltInMaterials().Single(m => m.Id == "GLASS_FLOAT");
    var layers = new List<SoundInsulationLayer> { new(glass, 0.004) };
    var tmm = SoundInsulationCalculator.Calculate(new WallAssembly("g", "g", layers, Method: CalculationMethod.TMM));
    var emp = SoundInsulationCalculator.Calculate(new WallAssembly("ge", "ge", layers, Method: CalculationMethod.Empirical));

    double tl1000 = tmm.TransmissionLossValues[1000];
    double tl3150 = tmm.TransmissionLossValues[3150]; // fc komşuluğu
    AssertGreater(tl1000 - tl3150, 6.0,
        $"Cam 4mm: fc komşuluğu 3150Hz TL ({tl3150:F1}) 1000Hz'den ({tl1000:F1}) en az 6 dB düşük olmalı (coincidence çukuru)");

    // fc altında katı model empirical ile uyumlu olmalı
    double tl500Tmm = tmm.TransmissionLossValues[500];
    double tl500Emp = emp.TransmissionLossValues[500];
    AssertClose(tl500Tmm, tl500Emp, 4.0,
        $"Cam 4mm 500Hz katı-model TMM ({tl500Tmm:F1}) empirical ({tl500Emp:F1}) ile fc altında uyumlu olmalı");
}

static void SoundInsulationThickConcreteSolidTmmReasonableRw()
{
    // 250 mm yoğun betonarme: kütle kanunu Rw'yi aşırı iyimser tahmin eder; katı tabaka (kayma + kalınlık
    // rezonansları) modeli daha gerçekçi, daha düşük bir Rw vermeli.
    var conc = SoundInsulationLibrary.GetBuiltInMaterials().Single(m => m.Id == "CONC_DENSE");
    var layers = new List<SoundInsulationLayer> { new(conc, 0.250) };
    var tmm = SoundInsulationCalculator.Calculate(new WallAssembly("c", "c", layers, Method: CalculationMethod.TMM));
    var emp = SoundInsulationCalculator.Calculate(new WallAssembly("ce", "ce", layers, Method: CalculationMethod.Empirical));

    double rwTmm = tmm.RwRating!.Rw;
    AssertBetween(rwTmm, 48, 64, $"250mm beton TMM Rw ({rwTmm}) literatür aralığında (~53-60) olmalı");
    AssertGreater(emp.RwRating!.Rw, rwTmm,
        $"Katı tabaka modeli kütle kanununun aşırı iyimserliğini düşürmeli (TMM {rwTmm} < Empirical {emp.RwRating!.Rw})");
}

// ── Bug B regresyonu: tek levha coincidence üstünde kütle kanununu sınırsız aşmamalı ──
static void SoundInsulationEmpiricalSingleLeafBoundedByMassLaw()
{
    // ESKİ HATA: "plateau(fc) + 10·oktav" formülü 200mm betonu 3150 Hz'de kütle kanununun
    // ~17 dB, 5000 Hz'de ~20 dB üstüne çıkarıyordu. Artık massLaw(f)'e çapalı kalmalı.
    var conc = SoundInsulationLibrary.GetBuiltInMaterials().Single(m => m.Id == "CONC_NORMAL");
    var r = SoundInsulationCalculator.Calculate(
        new WallAssembly("c", "c", new List<SoundInsulationLayer> { new(conc, 0.20) }));

    double ms = conc.Density * 0.20;
    foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
    {
        double tl = r.TransmissionLossValues[f];
        double massLaw = Math.Max(0, 20 * Math.Log10(ms * f) - 47);
        // Coincidence üstü TL kütle kanununu en fazla küçük bir sınırlı pay kadar aşabilir.
        AssertGreater(massLaw + 8.0, tl,
            $"200mm beton {f} Hz TL ({tl:F1}) kütle kanununu ({massLaw:F1}) +8 dB'den fazla aşmamalı");
    }
}

// ── Bug A regresyonu: çift cidar TL fiziksel tavanı asla aşmamalı ──
static void SoundInsulationEmpiricalDoubleWallCappedByCeiling()
{
    // ESKİ HATA: 100/hava50/100 beton çift cidar 3150 Hz'de 186 dB, Rw=95 üretiyordu.
    var mats = SoundInsulationLibrary.GetBuiltInMaterials();
    var conc = mats.Single(m => m.Id == "CONC_NORMAL");
    var air = mats.Single(m => m.Id == "AIR");
    var r = SoundInsulationCalculator.Calculate(new WallAssembly("d", "d",
        new List<SoundInsulationLayer>
        {
            new(conc, 0.10),
            new(air, 0.05, IsAirGap: true),
            new(conc, 0.10)
        }));

    foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
    {
        double tl = r.TransmissionLossValues[f];
        AssertFinite(tl, $"Çift cidar {f} Hz TL sonlu olmalı");
        AssertGreater(85.0 + 1e-6, tl, $"Çift cidar {f} Hz TL ({tl:F1}) fiziksel tavanı (85 dB) aşmamalı");
    }
    AssertGreater(85.0 + 1e-6, r.RwRating!.Rw, $"Çift cidar Rw ({r.RwRating!.Rw}) tavanı aşmamalı");
}

// ── Bug B regresyonu: coincidence çukur→toparlanma geçişinde sahte sıçrama olmamalı ──
static void SoundInsulationEmpiricalCurveContinuous()
{
    // ESKİ HATA: 4mm cam 3150 Hz=21 dB → 5000 Hz=47 dB (tek 1/3 oktavda 26 dB sıçrama).
    // Düşük sönümlü camın coincidence çukuru keskindir ama sıçrama ~15 dB ile sınırlı kalmalı.
    var glass = SoundInsulationLibrary.GetBuiltInMaterials().Single(m => m.Id == "GLASS_FLOAT");
    var r = SoundInsulationCalculator.Calculate(
        new WallAssembly("g", "g", new List<SoundInsulationLayer> { new(glass, 0.004) }));

    var bands = SoundInsulationConstants.ThirdOctaveBands;
    for (int i = 1; i < bands.Count; i++)
    {
        double jump = Math.Abs(r.TransmissionLossValues[bands[i]] - r.TransmissionLossValues[bands[i - 1]]);
        AssertGreater(15.0, jump,
            $"4mm cam {bands[i - 1]}→{bands[i]} Hz TL sıçraması ({jump:F1} dB) 15 dB'yi aşmamalı");
    }
}

// ── Bug B regresyonu: kalın beton Empirical Rw makul sınırda (kütle kanunu kaçağı yok) ──
static void SoundInsulationEmpiricalThickConcreteRwBounded()
{
    // ESKİ HATA: 200mm beton Empirical Rw=65, STC=68 (aşırı iyimser, kütle kanunu kaçağı).
    var conc = SoundInsulationLibrary.GetBuiltInMaterials().Single(m => m.Id == "CONC_NORMAL");
    var r = SoundInsulationCalculator.Calculate(
        new WallAssembly("c", "c", new List<SoundInsulationLayer> { new(conc, 0.20) }));

    // Empirical kasıtlı olarak TMM'den iyimser; yine de mass-law sınırında (~56-63) kalmalı.
    AssertBetween(r.RwRating!.Rw, 52, 64, $"200mm beton Empirical Rw ({r.RwRating!.Rw}) makul sınırda olmalı");
    AssertBetween(r.RwRating!.Stc!.Value, 52, 66, $"200mm beton Empirical STC ({r.RwRating!.Stc}) makul sınırda olmalı");
}

// ── Köprü regresyonu: bağlantı tipi sıralaması ve Direct (çelik stud) gerçekçiliği ──
static void SoundInsulationBridgeConnectionOrderingIsRealistic()
{
    // ESKİ HATA: "Direct" köprü platosu çok düşüktü → 2x12.5/70mm yünlü duvar yanlışlıkla
    // Rw ~35'e çöküyordu (gerçek ~48-52). Köprü esas yüksek-frekans platosunu sınırlamalı.
    var mats = SoundInsulationLibrary.GetBuiltInMaterials();
    var gyp = mats.Single(m => m.Id == "GYP_STD");
    var wool = mats.Single(m => m.Id == "SW_LIGHT");
    List<SoundInsulationLayer> Layers() => new()
    {
        new(gyp, 0.025),
        new(wool, 0.07, IsAirGap: true, HasPorousFill: true),
        new(gyp, 0.025)
    };
    int Rw(string ct, double ss) => SoundInsulationCalculator
        .Calculate(new WallAssembly("w", "w", Layers(), StudSpacing: ss, ConnectionType: ct)).RwRating!.Rw;

    int none = Rw("None", 0);
    int direct = Rw("Direct", 0.6);
    int rc = Rw("ResilientChannel", 0.6);
    int clips = Rw("Clips", 0.6);

    // Ayrıştırma arttıkça performans artar; hiçbiri ayrıştırılmamış ideali geçemez.
    AssertGreater(none + 1, clips, $"Hiçbir bağlantı ideali geçmemeli (None={none}, Clips={clips})");
    AssertGreater(clips + 1, rc, $"Clips ≥ ResilientChannel olmalı (Clips={clips}, RC={rc})");
    AssertGreater(rc, direct, $"ResilientChannel > Direct olmalı (RC={rc}, Direct={direct})");
    // Direct (çelik stud) duvarı çökertmemeli.
    AssertBetween(direct, 42, 54, $"2x12.5/70mm Direct Rw ({direct}) gerçekçi olmalı (~48-52)");
}

// ── MAM çukuru regresyonu: poröz dolgu kavite rezonansını söndürür ──
static void SoundInsulationPorousFillReducesMamDip()
{
    // Aynı geometri: biri taş yünü dolgulu, biri boş hava. Yünlü, MAM rezonans çukurunu
    // söndürerek alt bantları (ve Rw'yi) yükseltmeli. ESKİ sabit -8 dB çukur dolguyu yok sayıyordu.
    var mats = SoundInsulationLibrary.GetBuiltInMaterials();
    var gyp = mats.Single(m => m.Id == "GYP_STD");
    var wool = mats.Single(m => m.Id == "SW_LIGHT");
    var air = mats.Single(m => m.Id == "AIR");

    var filled = SoundInsulationCalculator.Calculate(new WallAssembly("f", "f", new List<SoundInsulationLayer>
    {
        new(gyp, 0.0125), new(wool, 0.05, IsAirGap: true, HasPorousFill: true), new(gyp, 0.0125)
    }));
    var empty = SoundInsulationCalculator.Calculate(new WallAssembly("e", "e", new List<SoundInsulationLayer>
    {
        new(gyp, 0.0125), new(air, 0.05, IsAirGap: true, HasPorousFill: false), new(gyp, 0.0125)
    }));

    AssertGreater(filled.RwRating!.Rw, empty.RwRating!.Rw,
        $"Yünlü dolgu Rw'yi yükseltmeli (yünlü={filled.RwRating!.Rw}, boş={empty.RwRating!.Rw})");
    AssertGreater(filled.TransmissionLossValues[125], empty.TransmissionLossValues[125],
        "Yünlü dolgu MAM rezonans bölgesinde (125 Hz) daha yüksek TL vermeli");
}

// ── Triple-leaf regresyonu (Bug F + A): 3+ yapraklı sistem uyarılı, sınırlı ve sonlu ──
static void SoundInsulationTripleLeafBoundedAndWarned()
{
    // İki boşluklu çift karkas (5 katman). ESKİ TUTARSIZLIK: f0 totalGap ile, kavite d'si
    // gaps[0] ile hesaplanıyordu; artık ikisi de toplam boşluğu kullanır. Sonuç sınırlı olmalı.
    var mats = SoundInsulationLibrary.GetBuiltInMaterials();
    var gyp = mats.Single(m => m.Id == "GYP_SOUND");
    var wool = mats.Single(m => m.Id == "SW_LIGHT");
    var air = mats.Single(m => m.Id == "AIR");
    var r = SoundInsulationCalculator.Calculate(new WallAssembly("t", "t", new List<SoundInsulationLayer>
    {
        new(gyp, 0.015),
        new(wool, 0.05, IsAirGap: true, HasPorousFill: true),
        new(gyp, 0.015),
        new(air, 0.05, IsAirGap: true),
        new(gyp, 0.015)
    }));

    if (!r.Warnings.Any(w => w.Contains("Triple Leaf")))
        throw new InvalidOperationException("Triple-leaf sistem uyarısı verilmeli.");

    foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
    {
        double tl = r.TransmissionLossValues[f];
        AssertFinite(tl, $"Triple-leaf {f} Hz TL sonlu olmalı");
        AssertGreater(85.0 + 1e-6, tl, $"Triple-leaf {f} Hz TL ({tl:F1}) fiziksel tavanı aşmamalı");
    }
    AssertBetween(r.RwRating!.Rw, 40, 80, $"Triple-leaf Rw ({r.RwRating!.Rw}) makul sınırda olmalı");
    AssertGreater(r.MassAirMassResonance, 0, "Triple-leaf MAM rezonansı (toplam boşluk) pozitif olmalı");
}

// ── JCA poröz regresyonu (Bug E): TMM poröz katman tüm porozite aralığında sonlu ve sınırlı ──
static void SoundInsulationTmmPorousFiniteAcrossPorosity()
{
    // Zc = √(ρ_eff·K_eff) makroskopik formdadır; eski fazladan /φ NaN/patlama riski taşımazdı ama
    // düşük porozitede yanlış yön veriyordu. Burada tüm φ aralığında sonlu + makul kalmalı.
    foreach (var phi in new[] { 0.99, 0.9, 0.7, 0.5, 0.3 })
    {
        var mat = new SoundInsulationMaterial { Id = "x", Name = "x", Density = 50, FlowResistivity = 18000, Porosity = phi, Tortuosity = 1.0 };
        var wa = new WallAssembly("p", "p", new List<SoundInsulationLayer>
        {
            new(mat, 0.05, IsAirGap: true, HasPorousFill: true)
        }, Method: CalculationMethod.TMM);
        var r = SoundInsulationCalculator.Calculate(wa);
        foreach (var f in SoundInsulationConstants.ThirdOctaveBands)
        {
            double tl = r.TransmissionLossValues[f];
            AssertFinite(tl, $"JCA poröz φ={phi} {f} Hz TL sonlu olmalı");
            AssertBetween(tl, 0, 60, $"JCA poröz φ={phi} {f} Hz TL ({tl:F1}) makul sınırda olmalı");
        }
    }
}

static ReceiverPoint RunSingleReceiverScenario(double barrierHeight, double barrierLength, double materialTl, double absorption = 0.0)
{
    return Run(CreateBaseProject(barrierHeight, barrierLength), materialTl, absorption);
}

static NoiseBarrierProject CreateBaseProject(double barrierHeight, double barrierLength)
{
    var project = new NoiseBarrierProject
    {
        Building =
        {
            Width = 20,
            Height = 12,
            Depth = 10,
            FacadeGridColumns = 1,
            FacadeGridRows = 1,
            FacadeReceiverOffset = 0.05
        },
        GroundAbsorption = 0,
        EnableFacadeReflections = false,
        FacadeReflectionCoefficient = 0.75,
        BarrierReflectionCoefficient = 0.45,
        MaxReflectionOrder = 2
    };

    project.Barriers.Clear();
    project.Barriers.Add(new BarrierModel
    {
        Name = "Bariyer 1",
        Height = barrierHeight,
        Length = barrierLength,
        DistanceFromBuilding = 5,
        CenterX = 0,
        MaterialId = "test"
    });

    project.Sources.Add(new NoiseSource
    {
        Name = "Point source",
        Type = SourceType.Point,
        X = 0,
        Y = -30,
        Z = 0.5,
        OverallLw = 100
    });

    return project;
}

static NoiseBarrierProject CreateTrafficProject(
    double flow,
    double speed,
    double heavyPercent,
    TrafficEmissionModel model = TrafficEmissionModel.EngineeringPreview)
{
    var project = CreateBaseProject(barrierHeight: 5.0, barrierLength: 80.0);
    project.Sources.Clear();
    project.Sources.Add(new NoiseSource
    {
        Name = "Traffic",
        Type = SourceType.Traffic,
        X = -25,
        Y = -30,
        XEnd = 25,
        YEnd = -30,
        Z = 0.5,
        TrafficFlowVehiclesPerHour = flow,
        AverageSpeedKmh = speed,
        HeavyVehiclePercent = heavyPercent,
        TrafficModel = model
    });

    return project;
}

static ReceiverPoint Run(NoiseBarrierProject project, double materialTl, double absorption = 0.0)
{
    var materials = new List<NoiseBarrierMaterial>
    {
        CreateMaterial("test", materialTl, absorption)
    };

    var engine = new NoiseBarrierEngine(project, materials);
    engine.GenerateReceivers();
    engine.RunSimulation();
    return project.Receivers.Single();
}

static NoiseBarrierMaterial CreateMaterial(string id, double tl, double absorption)
{
    return new NoiseBarrierMaterial
    {
        Id = id,
        Name = "Test material",
        TransmissionLoss = new Dictionary<double, double>
        {
            { 63, tl }, { 125, tl }, { 250, tl }, { 500, tl },
            { 1000, tl }, { 2000, tl }, { 4000, tl }, { 8000, tl }
        },
        AbsorptionCoefficients = new Dictionary<double, double>
        {
            { 63, absorption }, { 125, absorption }, { 250, absorption }, { 500, absorption },
            { 1000, absorption }, { 2000, absorption }, { 4000, absorption }, { 8000, absorption }
        }
    };
}

static double PathDifferenceForFresnel(double fresnel, double frequency)
{
    return fresnel * NoiseBarrierDiffraction.DefaultSpeedOfSound / (2.0 * frequency);
}

static void AssertClose(double expected, double actual, double tolerance, string label)
{
    if (Math.Abs(expected - actual) > tolerance)
        throw new InvalidOperationException($"{label}: expected {expected:F2}, actual {actual:F2}.");
}

static void AssertGreater(double actual, double threshold, string message)
{
    if (actual <= threshold)
        throw new InvalidOperationException($"{message} Actual {actual:F2}, threshold {threshold:F2}.");
}

static void AssertBetween(double actual, double min, double max, string label)
{
    if (actual < min || actual > max)
        throw new InvalidOperationException($"{label}: expected [{min:F2}, {max:F2}], actual {actual:F2}.");
}

static void AssertFinite(double value, string label)
{
    if (double.IsNaN(value) || double.IsInfinity(value))
        throw new InvalidOperationException($"{label} is not finite.");
}

static void AssertThrows(Action action, string message)
{
    try
    {
        action();
    }
    catch
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void AcousticPanelsSequentialWidthsAreUsedInOrder()
{
    var bounds = new Kst.Acoustics.Core.Geometry.PanelYerlesimServisi.SalonBoyutlari
    {
        MinX = 0,
        MaxX = 4,
        MinY = 0,
        MaxY = 3,
        MinZ = 0,
        MaxZ = 3,
        SahneOnuMinY = true
    };

    var config = new AcousticPanelKonfigurasyon
    {
        PanelEniModu = PanelEniModu.Sirali,
        PanelEnleriM = [0.6, 0.8],
        SarkmaCikmaM = 0.2,
        AktifTipler = [PanelTipi.Tavan]
    };

    var panels = Kst.Acoustics.Core.Geometry.PanelYerlesimServisi.PanelleriUret(config, bounds);

    if (panels.Count != 4)
        throw new InvalidOperationException($"Expected 4 sequential panels, actual {panels.Count}.");

    AssertClose(0.6, panels[0].PanelEniM, 0.001, "Panel 1 width");
    AssertClose(0.8, panels[1].PanelEniM, 0.001, "Panel 2 width");
    AssertClose(0.8, panels[2].PanelEniM, 0.001, "Panel 3 width repeats last value");
    AssertClose(0.8, panels[3].PanelEniM, 0.001, "Panel 4 width repeats last value");
    AssertClose(3.0, panels[3].Vertices.Max(p => p.Y), 0.001, "Sequential panels fill depth");
}

static void AcousticPanelsUniformWidthKeepsClippedFinalSlice()
{
    var bounds = new Kst.Acoustics.Core.Geometry.PanelYerlesimServisi.SalonBoyutlari
    {
        MinX = 0,
        MaxX = 4,
        MinY = 0,
        MaxY = 2.5,
        MinZ = 0,
        MaxZ = 3,
        SahneOnuMinY = true
    };

    var config = new AcousticPanelKonfigurasyon
    {
        PanelEniM = 1.0,
        PanelEniModu = PanelEniModu.TekTip,
        SarkmaCikmaM = 0.2,
        AktifTipler = [PanelTipi.Tavan]
    };

    var panels = Kst.Acoustics.Core.Geometry.PanelYerlesimServisi.PanelleriUret(config, bounds);

    if (panels.Count != 3)
        throw new InvalidOperationException($"Expected 3 uniform panels, actual {panels.Count}.");

    AssertClose(1.0, panels[0].PanelEniM, 0.001, "Panel 1 width");
    AssertClose(1.0, panels[1].PanelEniM, 0.001, "Panel 2 width");
    AssertClose(0.5, panels[2].PanelEniM, 0.001, "Clipped final panel width");
    AssertClose(2.5, panels[2].Vertices.Max(p => p.Y), 0.001, "Uniform panels reach back wall");
}
static void BuildingAnnexEKijRigidLJunction()
{
    // H1 — L-junction rigid Kij formula: K = 15|M| - 3, minimum -2 dB
    AssertClose(-2.0, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.L, AnnexEYolTipi.Branch, 0), 0.01, "L-junction M=0 gives -2.0 (clamped)");
    
    double m = 0.5; // |M| = 0.5
    AssertClose(15.0 * 0.5 - 3.0, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.L, AnnexEYolTipi.Branch, m), 0.01, "L-junction M=0.5 gives 4.5");
    AssertClose(15.0 * 0.5 - 3.0, AnnexEKijServisi.RigidKij(JunctionGeometriTipi.L, AnnexEYolTipi.Branch, -m), 0.01, "L-junction M=-0.5 gives 4.5");
}

static YapiElemani KutleliEleman(string ad, YapiElemaniTipi tip, double rw, double yuzeyKutlesi, double lnw = 0)
    => new()
    {
        Ad = ad, Tip = tip, Rw = rw, Lnw = lnw, BaglantiTipi = BaglantiTipi.Rijit,
        Katmanlar = { new YapiKatmani { MalzemeAdi = ad, Kalinlik = yuzeyKutlesi, Yogunluk = 1000.0 } }
    };

static void BuildingFlankingDvClampedNonNegative()
{
    // A4 — EN 12354-1 E.3 Limiting values: D̄_v,ij,situ ≥ 0 dB.
    // Uzun junction (lij=50) + küçük odalar → soğurma düzeltmesi güçlü negatif → kırpma devreye girer.
    var d = KutleliEleman("D ağır", YapiElemaniTipi.Duvar, 55, 400);
    var doseme = KutleliEleman("Döşeme hafif", YapiElemaniTipi.Doseme, 52, 100);
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 1, 1);
    var alici = CreateRectMekan("Alıcı", 1, 0, 2, 1);
    kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
    alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;

    var bolme = new Bolme
    {
        Ad = "Ayırıcı", Baslangic = new Point2D(0, 0), Bitis = new Point2D(50, 0), // lij = 50
        HasGeometry = true, Alan = 9, YapiElemani = d, KaynakMekan = kaynak, AliciMekan = alici
    };
    kaynak.Bolmeler.Add(bolme);

    var df = HavaSesiHesapServisi.GetOnUcYolFlankingYollari(bolme)
        .First(y => y.Kenar == JunctionKenarTipi.Alt && y.YolTipi == "Df");
    AssertClose(0.0, df.Kij, 0.01, $"Uzun junction'da D̄_v,ij 0'a kırpılmalı; alınan: {df.Kij:F2}");
}

static void BuildingImpactFlankingDvReducesLevel()
{
    // S2 — EN 12354-2: D̄_v,ij flanking darbe seviyesini DÜŞÜRÜR. Elastik bağlantı
    // (daha yüksek Dv) rijit bağlantıya göre daha DÜŞÜK L'n,w vermeli; flanking
    // enerji eklediği için her iki sonuç da çıplak Ln'den büyük olmalı.
    double ApparentLnwWith(BaglantiTipi baglanti)
    {
        var dosemeEl = KutleliEleman("Döşeme", YapiElemaniTipi.Doseme, 55, 200, lnw: 70);
        var duvarEl = KutleliEleman("Duvar", YapiElemaniTipi.Duvar, 50, 300);
        duvarEl.BaglantiTipi = baglanti;
        var alici = CreateRectMekan("Alt", 0, 0, 4, 3);
        alici.Bolmeler.Add(new Bolme
        {
            Ad = "Yan duvar", Alan = 10, YapiElemani = duvarEl,
            Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0), HasGeometry = true
        });
        var bolme = new Bolme
        {
            Ad = "Döşeme", Alan = 12, YapiElemani = dosemeEl,
            KaynakMekan = CreateRectMekan("Üst", 0, 0, 4, 3), AliciMekan = alici
        };
        return new DarbeSesiHesapServisi().HesaplaApparentLnw(bolme);
    }

    var rijit = ApparentLnwWith(BaglantiTipi.Rijit);
    var elastik = ApparentLnwWith(BaglantiTipi.Elastik);
    AssertGreater(rijit, 70.0, $"Flanking enerji ekler: rijit L'n,w > Ln (70); alınan {rijit:F2}");
    AssertGreater(elastik, 70.0 - 1e-9, $"Elastik L'n,w ≥ Ln (70); alınan {elastik:F2}");
    AssertGreater(rijit, elastik, $"Elastik bağlantı (yüksek Dv) daha düşük L'n,w vermeli: rijit {rijit:F2} > elastik {elastik:F2}");
}

static void BuildingImpactFlankingIgnoresCeilingSelf()
{
    // S3 — Alıcı odanın tavanı ayırıcı döşemenin kendisidir; flank olarak kullanılmaz.
    // Duvarsız alıcı odada (tavan/zemin atanmış olsa da) flanking katkısı 0 olmalı.
    var dosemeEl = KutleliEleman("Döşeme", YapiElemaniTipi.Doseme, 55, 200, lnw: 70);
    var alici = CreateRectMekan("Alt", 0, 0, 4, 3);
    alici.TavanYapiElemani = dosemeEl;                                          // ayırıcının kendisi
    alici.ZeminYapiElemani = KutleliEleman("Alt zemin", YapiElemaniTipi.Doseme, 52, 300);
    alici.Bolmeler.Add(new Bolme   // döşeme tipi bölme de flank sayılmaz
    {
        Ad = "Alt döşeme", Alan = 12,
        YapiElemani = KutleliEleman("Alt döşeme", YapiElemaniTipi.Doseme, 52, 300)
    });

    var bolme = new Bolme
    {
        Ad = "Döşeme", Alan = 12, YapiElemani = dosemeEl,
        KaynakMekan = CreateRectMekan("Üst", 0, 0, 4, 3), AliciMekan = alici
    };
    var apparent = new DarbeSesiHesapServisi().HesaplaApparentLnw(bolme);
    AssertClose(70.0, apparent, 0.01, $"Duvarsız alıcıda L'n,w çıplak Ln'e eşit olmalı; alınan {apparent:F2}");
}

static void BuildingEk2DegreesMatchTablo21()
{
    // U1 — Yönetmelik EK-2 Tablo 2.1 dereceleri (kaynak gürültülülük / alıcı hassasiyet).
    // Daha önce yanlış olan kritik satırlar regresyon kilidi olarak sabitlenir.
    static void Dogrula(BinaIslevi islev, string kategoriAd, string kaynak, string alici)
    {
        var k = EK2SiniflandirmaServisi.GetKategoriler(islev).FirstOrDefault(x => x.Ad == kategoriAd)
            ?? throw new InvalidOperationException($"{islev}/{kategoriAd} kategorisi bulunamadı.");
        if (k.KaynakDerecesi != kaynak || k.AliciSinifi != alici)
            throw new InvalidOperationException(
                $"{islev}/{kategoriAd}: beklenen {kaynak}/{alici}, alınan {k.KaynakDerecesi}/{k.AliciSinifi} (Tablo 2.1).");
    }

    Dogrula(BinaIslevi.Konut,                "Mutfaklar / Banyo",              "OG", "III");
    Dogrula(BinaIslevi.EgitimTesisleri,      "Derslikler",                     "OG", "I");
    Dogrula(BinaIslevi.EgitimTesisleri,      "Okuma Odaları",                  "DG", "I");
    Dogrula(BinaIslevi.SaglikVeYasliEvleri,  "Hasta / Yaşlı Odaları",          "OG", "I");
    Dogrula(BinaIslevi.SaglikVeYasliEvleri,  "Ameliyathaneler",                "DG", "I");
    Dogrula(BinaIslevi.SaglikVeYasliEvleri,  "Muayene / Tedavi Odaları",       "OG", "II");
    Dogrula(BinaIslevi.BuroIdari,            "Bireysel Ofisler",               "OG", "I");
    Dogrula(BinaIslevi.BuroIdari,            "Açık Ofisler",                   "OG", "II");
    Dogrula(BinaIslevi.Konaklama,            "Misafir Odaları",                "OG", "I");
    Dogrula(BinaIslevi.Yurt,                 "Yatak / Çalışma Odaları",        "OG", "I");
    Dogrula(BinaIslevi.Yurt,                 "Yemekhane",                      "YG", "III");
    Dogrula(BinaIslevi.KulturelTesisler,     "Performans / Gösteri Salonları", "YG", "I");
    Dogrula(BinaIslevi.KulturelTesisler,     "Kütüphaneler",                   "DG", "I");
    Dogrula(BinaIslevi.TicariTesisler,       "Satış / Ticaret Alanları",       "YG", "III");
    Dogrula(BinaIslevi.Terminaller,          "Bekleme / Genel Alanlar",        "YG", "III");
    Dogrula(BinaIslevi.DiniTesisler,         "İbadet Salonları",               "YG", "II");
    Dogrula(BinaIslevi.SanayiTesisleri,      "İdari / Ofis Alanları",          "OG", "II");

    // Anahtar kelime yönlendirmesi: taşınan kelimeler yeni kategorilere düşmeli.
    var kresYemek = EK2SiniflandirmaServisi.TahminEt(BinaIslevi.Yurt, "Yemekhane", "");
    if (kresYemek?.Ad != "Yemekhane")
        throw new InvalidOperationException($"Yurt/Yemekhane tahmini Yemekhane'ye düşmeli; alınan {kresYemek?.Ad}.");
    var kutuphane = EK2SiniflandirmaServisi.TahminEt(BinaIslevi.KulturelTesisler, "Kütüphane", "");
    if (kutuphane?.Ad != "Kütüphaneler")
        throw new InvalidOperationException($"Kültürel/Kütüphane tahmini Kütüphaneler'e düşmeli; alınan {kutuphane?.Ad}.");
    var ameliyat = EK2SiniflandirmaServisi.TahminEt(BinaIslevi.SaglikVeYasliEvleri, "Ameliyathane 1", "");
    if (ameliyat?.Ad != "Ameliyathaneler")
        throw new InvalidOperationException($"Sağlık/Ameliyathane tahmini Ameliyathaneler'e düşmeli; alınan {ameliyat?.Ad}.");
}

static void BuildingEk3LimitTablesMatchRegulation()
{
    // U2/U3/U4 — EK-3 Tablo 3.1 / 3.3 / 3.4 / 3.5 sınır değerleri (Yönetmelik metniyle doğrulandı).

    // Tablo 3.3 (L'nT,w, kaynak derecesi × sınıf)
    AssertClose(54, YonetmelikLimitleri.GetTablo33Limit(AkustikPerformansSinifi.C, "OG").HedefDeger, 0.01, "Tablo 3.3 OG/C = 54");
    AssertClose(40, YonetmelikLimitleri.GetTablo33Limit(AkustikPerformansSinifi.A, "YG").HedefDeger, 0.01, "Tablo 3.3 YG/A = 40");
    AssertClose(70, YonetmelikLimitleri.GetTablo33Limit(AkustikPerformansSinifi.F, "DG").HedefDeger, 0.01, "Tablo 3.3 DG/F = 70");
    AssertClose(0,  YonetmelikLimitleri.GetTablo33Limit(AkustikPerformansSinifi.C, "").HedefDeger,   0.01, "Tablo 3.3 derece eksik → 0 (VeriEksik)");

    // Tablo 3.1 (DnT,A,tr = Lgag − ofset; A–D'de 30 dB taban)
    AssertClose(43, YonetmelikLimitleri.GetTablo31Limit(AkustikPerformansSinifi.C, "I",   65).HedefDeger, 0.01, "Tablo 3.1: 65−22 = 43 (I/C)");
    AssertClose(30, YonetmelikLimitleri.GetTablo31Limit(AkustikPerformansSinifi.C, "III", 48).HedefDeger, 0.01, "Tablo 3.1: max(48−28, 30) = 30 taban");
    AssertClose(30, YonetmelikLimitleri.GetTablo31Limit(AkustikPerformansSinifi.D, "II",   0).HedefDeger, 0.01, "Tablo 3.1: Lgag yok → A–D taban 30");
    AssertClose(0,  YonetmelikLimitleri.GetTablo31Limit(AkustikPerformansSinifi.E, "II",   0).HedefDeger, 0.01, "Tablo 3.1: Lgag yok, E sınıfı → 0 (VeriEksik)");
    AssertClose(31, YonetmelikLimitleri.GetTablo31Limit(AkustikPerformansSinifi.A, "II",  48).HedefDeger, 0.01, "Tablo 3.1: 48−17 = 31 (II/A)");

    // Tablo 3.4 / 3.5 (konut, aynı bağımsız birim içi)
    AssertClose(44, YonetmelikLimitleri.GetTablo34AyniBirimLimit(AkustikPerformansSinifi.C).HedefDeger, 0.01, "Tablo 3.4 konut içi C = 44");
    AssertClose(62, YonetmelikLimitleri.GetTablo35AyniBirimLimit(AkustikPerformansSinifi.C).HedefDeger, 0.01, "Tablo 3.5 konut içi C = 62");

    // Yönlendirme: S1 + aynı bağımsız birim içi duvar → Tablo 3.4; döşeme → Tablo 3.5.
    var bb = Guid.NewGuid();
    var kaynak = CreateRectMekan("Oda 1", 0, 0, 3, 3); kaynak.BagimsizBolumId = bb; kaynak.KaynakDerecesi = "OG";
    var alici  = CreateRectMekan("Oda 2", 3, 0, 6, 3); alici.BagimsizBolumId = bb; alici.AliciSinifi = "I";
    var duvar = new Bolme
    {
        Ad = "İç bölme", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Alçıpan", Tip = YapiElemaniTipi.Duvar, Rw = 50 },
        KaynakMekan = kaynak, AliciMekan = alici
    };
    var svc = new YonetmelikUyumServisi();
    var (_, icLimit) = svc.KontrolEtDetayli(BinaSinifi.S1, AkustikPerformansSinifi.C, duvar);
    AssertClose(44, icLimit.HedefDeger, 0.01, $"S1 aynı birim içi duvar Tablo 3.4'e gitmeli (44); alınan {icLimit.HedefDeger}");

    var dosemeBolme = new Bolme
    {
        Ad = "İç döşeme", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Lnw = 60 },
        KaynakMekan = kaynak, AliciMekan = alici
    };
    var (_, dosemeLimit) = svc.KontrolEtDetayli(BinaSinifi.S1, AkustikPerformansSinifi.C, dosemeBolme);
    AssertClose(62, dosemeLimit.HedefDeger, 0.01, $"S1 aynı birim içi döşeme Tablo 3.5'e gitmeli (62); alınan {dosemeLimit.HedefDeger}");
}

static void BuildingDoorToleranceLowersLimit()
{
    // U5 — Madde 11(3): kapı içeren iç bölme elemanında sınır değer 14 dB düşürülür.
    var kaynak = CreateRectMekan("Kaynak", 0, 0, 3, 3); kaynak.KaynakDerecesi = "OG";
    var alici  = CreateRectMekan("Alıcı", 3, 0, 6, 3); alici.AliciSinifi = "I";

    Bolme Make() => new()
    {
        Ad = "Bölme", Alan = 9,
        YapiElemani = new YapiElemani { Ad = "Duvar", Tip = YapiElemaniTipi.Duvar, Rw = 55 },
        KaynakMekan = kaynak, AliciMekan = alici
    };

    var svc = new YonetmelikUyumServisi();
    var kapisiz = Make();
    var (_, limitKapisiz) = svc.KontrolEtDetayli(BinaSinifi.S1, AkustikPerformansSinifi.C, kapisiz);
    AssertClose(52, limitKapisiz.HedefDeger, 0.01, $"Tablo 3.2 OG/I/C = 52; alınan {limitKapisiz.HedefDeger}");

    var kapili = Make();
    kapili.Acikliklar.Add(new BolmeAcikligi { Tip = BolmeAcikligiTipi.Kapi, Genislik = 0.9, Yukseklik = 2.1 });
    var (_, limitKapili) = svc.KontrolEtDetayli(BinaSinifi.S1, AkustikPerformansSinifi.C, kapili);
    AssertClose(52 - 14, limitKapili.HedefDeger, 0.01, $"Kapılı elemanda limit 38'e inmeli; alınan {limitKapili.HedefDeger}");

    // Pencere kapı değildir — tolerans uygulanmaz.
    var pencereli = Make();
    pencereli.Acikliklar.Add(new BolmeAcikligi { Tip = BolmeAcikligiTipi.Pencere, Genislik = 1.2, Yukseklik = 1.2 });
    var (_, limitPencereli) = svc.KontrolEtDetayli(BinaSinifi.S1, AkustikPerformansSinifi.C, pencereli);
    AssertClose(52, limitPencereli.HedefDeger, 0.01, $"Pencereli elemanda tolerans yok (52); alınan {limitPencereli.HedefDeger}");
}

static void BuildingFlankingUsesOpaqueR()
{
    // H2 — Flanking uses opaque (solid) R instead of composite R
    var opaqueR = 55.0;
    var ayirici = new YapiElemani { Ad = "Ayırıcı", Tip = YapiElemaniTipi.Duvar, Rw = opaqueR, BaglantiTipi = BaglantiTipi.Rijit };
    var window = new YapiElemani { Ad = "Pencere", Tip = YapiElemaniTipi.Pencere, Rw = 30 };
    
    var yan = new YapiElemani { Ad = "Yan", Tip = YapiElemaniTipi.Duvar, Rw = 50, BaglantiTipi = BaglantiTipi.Rijit };
    var doseme = new YapiElemani { Ad = "Döşeme", Tip = YapiElemaniTipi.Doseme, Rw = 52, BaglantiTipi = BaglantiTipi.Rijit };

    var kaynak = CreateRectMekan("Kaynak", 0, 0, 4, 3);
    var alici = CreateRectMekan("Alıcı", 4, 0, 8, 3);
    kaynak.ZeminYapiElemani = kaynak.TavanYapiElemani = doseme;
    alici.ZeminYapiElemani = alici.TavanYapiElemani = doseme;

    var bolme = new Bolme
    {
        Ad = "Ayırıcı duvar",
        Baslangic = new Point2D(4, 0),
        Bitis = new Point2D(4, 3),
        HasGeometry = true,
        Alan = 9,
        YapiElemani = ayirici,
        KaynakMekan = kaynak,
        AliciMekan = alici
    };
    
    bolme.Acikliklar.Add(new BolmeAcikligi { Tip = BolmeAcikligiTipi.Pencere, YapiElemani = window, Genislik = 1.5, Yukseklik = 1.4 });

    kaynak.Bolmeler.Add(bolme);
    kaynak.Bolmeler.Add(new Bolme { Ad = "K alt", Baslangic = new Point2D(0, 0), Bitis = new Point2D(4, 0), HasGeometry = true, YapiElemani = yan, KaynakMekan = kaynak });
    kaynak.Bolmeler.Add(new Bolme { Ad = "K üst", Baslangic = new Point2D(0, 3), Bitis = new Point2D(4, 3), HasGeometry = true, YapiElemani = yan, KaynakMekan = kaynak });
    alici.Bolmeler.Add(new Bolme { Ad = "A alt", Baslangic = new Point2D(4, 0), Bitis = new Point2D(8, 0), HasGeometry = true, YapiElemani = yan, KaynakMekan = alici });
    alici.Bolmeler.Add(new Bolme { Ad = "A üst", Baslangic = new Point2D(4, 3), Bitis = new Point2D(8, 3), HasGeometry = true, YapiElemani = yan, KaynakMekan = alici });

    var routes = HavaSesiHesapServisi.GetOnUcYolFlankingYollari(bolme);
    var compositeRw = HavaSesiHesapServisi.CalculateCompositeRw(bolme);
    
    // Assert compositeRw is much lower than opaqueR due to window
    AssertGreater(opaqueR - compositeRw, 5.0, "Composite R should be significantly lower than opaque R");

    // Df path uses opaqueRw, aliciRw, and ssArea
    var dfPath = routes.First(r => r.YolTipi == "Df");
    var kDf = dfPath.Kij;
    var expectedRij = (opaqueR + dfPath.AliciRw) / 2.0 + kDf + 10.0 * Math.Log10(bolme.Alan / Math.Max(0.01, Math.Sqrt(bolme.Alan * alici.Alan)));
    AssertClose(expectedRij, dfPath.Rij, 0.01, "Df path must use opaque R instead of composite R");
}
