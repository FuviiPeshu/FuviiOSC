using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using FuviiOSC.AvatarChanger.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.SDK.Parameters.Queryable;
using VRCOSC.App.UI.Views.Modules.Settings;
using VRCOSC.App.Utils;

namespace FuviiOSC.AvatarChanger;

public enum AvatarScaleMode
{
    None,
    MatchPrevious,
    FixedHeight
}

public class AvatarChangerModuleSetting : ListModuleSetting<AvatarChangerTrigger>
{
    public AvatarChangerModuleSetting()
        : base("Trigger list", "Add, edit or remove avatar change triggers", typeof(AvatarChangerModuleSettingView), [])
    {
    }

    protected override AvatarChangerTrigger CreateItem() => new();
}

public sealed class AvatarHeightPresetSetting : StringListModuleSetting
{
    public const int MaximumPresetCount = 10;

    public AvatarHeightPresetSetting(string title, string description, IEnumerable<float> defaultValues)
        : base(title, description, typeof(AvatarHeightPresetSettingView), defaultValues.Select(value => value.ToString(CultureInfo.InvariantCulture)))
    {
    }

    internal static bool TryParseHeight(string value, out float height)
    {
        string normalizedValue = value.Replace(',', '.');

        return float.TryParse(normalizedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out height) &&
               float.IsFinite(height);
    }

    public override void Add()
    {
        if (Attribute.Count >= MaximumPresetCount) return;

        base.Add();
    }

    protected override bool Deserialise(object? ingestValue)
    {
        if (ingestValue is not JArray jArrayValue) return false;

        return base.Deserialise(new JArray(jArrayValue.Take(MaximumPresetCount)));
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class AvatarChangerTrigger : IEquatable<AvatarChangerTrigger>
{
    [JsonProperty("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public Observable<string> Name { get; set; } = new("New trigger");

    [JsonProperty("avatar_id")]
    public Observable<string> AvatarId { get; set; } = new("avtr_");

    [JsonProperty("icon_key")]
    public Observable<string> IconKey { get; set; } = new(string.Empty);

    [JsonProperty("scale_mode")]
    public Observable<int> ScaleMode { get; set; } = new((int)AvatarScaleMode.None);

    [JsonProperty("fixed_eye_height")]
    public Observable<float> FixedEyeHeight { get; set; } = new(1.28f);

    [JsonProperty("trigger_params")]
    public ObservableCollection<TriggerQueryableParameter> TriggerParams { get; set; } = [];

    [JsonConstructor]
    public AvatarChangerTrigger()
    {
    }

    public bool Equals(AvatarChangerTrigger? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name.Equals(other.Name) && AvatarId.Equals(other.AvatarId) && TriggerParams.SequenceEqual(other.TriggerParams);
    }
}

public class TriggerQueryableParameter : QueryableParameter
{
}
