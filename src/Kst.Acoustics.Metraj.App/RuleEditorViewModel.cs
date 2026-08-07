using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Kst.Acoustics.Metraj.Rules;
using Kst.Acoustics.Metraj.Filters;

namespace Kst.Acoustics.Metraj.App;

public class RuleEditorViewModel : INotifyPropertyChanged
{
    private string _dwgPath;
    private RuleSet _ruleSet;

    public ObservableCollection<MetrajRule> Rules { get; } = new();
    public List<FilterLogic> Combinators { get; } = Enum.GetValues<FilterLogic>().ToList();

    public ICommand AddRuleCommand { get; }
    public ICommand RemoveRuleCommand { get; }
    public ICommand SaveCommand { get; }

    public RuleEditorViewModel(string dwgPath)
    {
        _dwgPath = dwgPath;
        _ruleSet = RuleSetStore.Load(dwgPath);
        
        foreach (var rule in _ruleSet.Rules)
        {
            Rules.Add(rule);
        }

        AddRuleCommand = new RelayCommand(AddRule);
        RemoveRuleCommand = new RelayCommand<MetrajRule>(RemoveRule);
        SaveCommand = new RelayCommand(Save);
    }

    private void AddRule()
    {
        Rules.Add(new MetrajRule { Name = "Yeni Kural" });
    }

    private void RemoveRule(MetrajRule? rule)
    {
        if (rule != null) Rules.Remove(rule);
    }

    private void Save()
    {
        _ruleSet.Rules = Rules.ToList();
        RuleSetStore.Save(_dwgPath, _ruleSet);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
