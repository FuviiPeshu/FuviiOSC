using System;
using System.Collections.Generic;
using System.Linq;
using FuviiOSC.Haptickle;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.Utils;

namespace FuviiOSC.Common;

public static class FuviiCommonUtils
{
    public static bool IsParameterActuallyValid(VRChatParameter param, HapticTriggerQueryableParameter queryableParameter)
    {
        return param.Type switch
        {
            ParameterType.Float => EvaluateFloatComparison(
                param.GetValue<float>(),
                queryableParameter.FloatValue.Value,
                queryableParameter.Comparison.Value),

            ParameterType.Int => EvaluateIntComparison(
                param.GetValue<int>(),
                queryableParameter.IntValue.Value,
                queryableParameter.Comparison.Value),

            ParameterType.Bool => EvaluateBoolComparison(
                param.GetValue<bool>(),
                queryableParameter.BoolValue.Value,
                queryableParameter.Comparison.Value),

            _ => false
        };
    }

    private static bool EvaluateFloatComparison(float value, float threshold, ComparisonOperation operation)
    {
        return operation switch
        {
            ComparisonOperation.GreaterThan => value > threshold,
            ComparisonOperation.LessThan => value < threshold,
            ComparisonOperation.GreaterThanOrEqualTo => value >= threshold,
            ComparisonOperation.LessThanOrEqualTo => value <= threshold,
            ComparisonOperation.EqualTo => Math.Abs(value - threshold) < float.Epsilon,
            ComparisonOperation.NotEqualTo => Math.Abs(value - threshold) >= float.Epsilon,
            _ => false
        };
    }

    private static bool EvaluateIntComparison(int value, int threshold, ComparisonOperation operation)
    {
        return operation switch
        {
            ComparisonOperation.GreaterThan => value > threshold,
            ComparisonOperation.LessThan => value < threshold,
            ComparisonOperation.GreaterThanOrEqualTo => value >= threshold,
            ComparisonOperation.LessThanOrEqualTo => value <= threshold,
            ComparisonOperation.EqualTo => value == threshold,
            ComparisonOperation.NotEqualTo => value != threshold,
            _ => false
        };
    }

    private static bool EvaluateBoolComparison(bool value, bool threshold, ComparisonOperation operation)
    {
        return operation switch
        {
            ComparisonOperation.EqualTo => value == threshold,
            ComparisonOperation.NotEqualTo => value != threshold,
            _ => false
        };
    }

    public static class EnumValuesGetter<T> where T : struct, Enum
    {
        public static IEnumerable<T> AllValues => Enum.GetValues(typeof(T)).Cast<T>();
    }
}

public static class ParameterTypeHelper
{
    public static IEnumerable<ParameterType> AllValues => FuviiCommonUtils.EnumValuesGetter<ParameterType>.AllValues;
}

public static class ComparisonOperationHelper
{
    public static IEnumerable<ComparisonOperation> AllValues => FuviiCommonUtils.EnumValuesGetter<ComparisonOperation>.AllValues;
}
