using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Osdu.Client.ExampleApp.Services;

namespace Osdu.Client.ExampleApp.Controls;

public class FilterConditionViewModel : INotifyPropertyChanged
{
    private readonly List<PropertyInfo> _allProperties;
    private string _propertyPath = "";
    private string _operator = "";
    private string _value = "";
    private bool _isEnabled = true;
    private PropertyInfo? _propertyInfo;

    public FilterConditionViewModel(List<PropertyInfo> properties)
    {
        _allProperties = properties;
        AvailableProperties = new ObservableCollection<string>(
            FlattenProperties(properties).Select(p => p.Path));
        Operators = new ObservableCollection<string>(GetStringOperators());
    }

    public string PropertyPath
    {
        get => _propertyPath;
        set { _propertyPath = value; OnPropertyChanged(); }
    }

    public string Operator
    {
        get => _operator;
        set { _operator = value; OnPropertyChanged(); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public PropertyInfo? PropertyInfo
    {
        get => _propertyInfo;
        set { _propertyInfo = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> AvailableProperties { get; }
    public ObservableCollection<string> Operators { get; }

    public void UpdateOperators()
    {
        Operators.Clear();
        var ops = PropertyInfo?.Kind switch
        {
            PropertyKind.Number or PropertyKind.DateTime => GetNumericOperators(),
            PropertyKind.Boolean => GetBooleanOperators(),
            _ => GetStringOperators()
        };
        foreach (var op in ops) Operators.Add(op);
        if (Operators.Count > 0 && string.IsNullOrEmpty(Operator))
            Operator = Operators[0];
    }

    public FilterCondition ToCondition() => new()
    {
        PropertyPath = PropertyPath,
        Operator = Operator,
        Value = Value,
        IsEnabled = IsEnabled,
        PropertyInfo = PropertyInfo
    };

    private static List<string> GetStringOperators() =>
        ["equals", "not equals", "contains", "does not contain", "starts with", "ends with", "is null", "is not null"];

    private static List<string> GetNumericOperators() =>
        ["equals", "not equals", "greater than", "greater than or equal", "less than", "less than or equal", "between", "is null", "is not null"];

    private static List<string> GetBooleanOperators() =>
        ["equals", "not equals", "is null", "is not null"];

    private static List<PropertyInfo> FlattenProperties(List<PropertyInfo> props, int maxDepth = 3)
    {
        var result = new List<PropertyInfo>();
        Flatten(props, result, maxDepth);
        return result;
    }

    private static void Flatten(List<PropertyInfo> props, List<PropertyInfo> result, int depth)
    {
        if (depth <= 0) return;
        foreach (var p in props)
        {
            if (p.Kind != PropertyKind.Object && p.Kind != PropertyKind.Array)
                result.Add(p);
            if (p.Children.Count > 0)
                Flatten(p.Children, result, depth - 1);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}