using System;
using System.Collections.Generic;
using System.Linq;
using FuviiOSC.Haptickle;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.Utils;

namespace FuviiOSC.Common;
 
public class FuviiCommonUtils
{
    public static bool IsParameterActuallyValid(VRChatParameter param, HapticTriggerQueryableParameter queryableParameter)
    {
        switch (param.Type)
        {
            case ParameterType.Float:
                {
                    float value = param.GetValue<float>();
                    float threshold = queryableParameter.FloatValue.Value;
                    switch (queryableParameter.Comparison.Value)
                    {
                        case ComparisonOperation.GreaterThan:
                            return value > threshold;
                        case ComparisonOperation.LessThan:
                            return value < threshold;
                        case ComparisonOperation.GreaterThanOrEqualTo:
                            return value >= threshold;
                        case ComparisonOperation.LessThanOrEqualTo:
                            return value <= threshold;
                        case ComparisonOperation.EqualTo:
                            return Math.Abs(value - threshold) < float.Epsilon;
                        case ComparisonOperation.NotEqualTo:
                            return Math.Abs(value - threshold) >= float.Epsilon;
                        default:
                            return false;
                    }
                }
            case ParameterType.Int:
                {
                    int value = param.GetValue<int>();
                    int threshold = queryableParameter.IntValue.Value;
                    switch (queryableParameter.Comparison.Value)
                    {
                        case ComparisonOperation.GreaterThan:
                            return value > threshold;
                        case ComparisonOperation.LessThan:
                            return value < threshold;
                        case ComparisonOperation.GreaterThanOrEqualTo:
                            return value >= threshold;
                        case ComparisonOperation.LessThanOrEqualTo:
                            return value <= threshold;
                        case ComparisonOperation.EqualTo:
                            return value == threshold;
                        case ComparisonOperation.NotEqualTo:
                            return value != threshold;
                        default:
                            return false;
                    }
                }
            case ParameterType.Bool:
                {
                    bool value = param.GetValue<bool>();
                    bool threshold = queryableParameter.BoolValue.Value;
                    switch (queryableParameter.Comparison.Value)
                    {
                        case ComparisonOperation.EqualTo:
                            return value == threshold;
                        case ComparisonOperation.NotEqualTo:
                            return value != threshold;
                        default:
                            return false;
                    }
                }
            default:
                return false;
        }
    }

    public static class EnumValuesGetter<T> where T : struct, Enum
    {
        public static IEnumerable<T> AllValues => Enum.GetValues(typeof(T)).Cast<T>();
    }
}
