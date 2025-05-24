using System;
using System.Collections.ObjectModel;
using System.Linq;
using FuviiOSC.AvatarChanger.UI;
using Newtonsoft.Json;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.SDK.Parameters.Queryable;
using VRCOSC.App.Utils;

namespace FuviiOSC.AvatarChanger;

public class AvatarChangerModuleSetting : ListModuleSetting<AvatarChangerTrigger>
{
    public AvatarChangerModuleSetting()
        : base("Trigger list", "Add, edit or remove avatar change triggers", typeof(AvatarChangerModuleSettingView), [])
    {
    }

    protected override AvatarChangerTrigger CreateItem() => new();
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
