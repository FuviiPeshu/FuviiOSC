using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Valve.VR;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters.Queryable;

namespace FuviiOSC.Haptickle;

[ModuleTitle("Haptickle")]
[ModuleDescription("Triggers Vive tracker haptics based on avatar parameters")]
[ModuleType(ModuleType.Generic)]
public class HaptickleModule : Module
{
	public CVRSystem? openVrSystem;

	[ModulePersistent("hapticTriggers")]
	public List<HapticTrigger> HapticTriggers { get; set; } = new();

	protected override Task<bool> OnModuleStart()
	{
		EVRInitError err = EVRInitError.None;
		try
		{
			openVrSystem = OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
			if (err != EVRInitError.None || openVrSystem == null)
			{
				throw new Exception($"OpenVR initialization failed with error: {err}");
			}
		}
		catch (Exception)
		{
			openVrSystem = null;
			return Task.FromResult(false);
		}
		return Task.FromResult(true);
	}

	protected override Task OnModuleStop()
	{
		if (openVrSystem != null)
		{
			OpenVR.Shutdown();
			openVrSystem = null;
		}

		return Task.CompletedTask;
	}

	public enum HaptickleSetting
	{
		HapticTriggers
	}

	public enum HaptickleParameter
	{
	}
}

public class HapticTrigger
{
	[JsonProperty("id")]
	public string ID { get; set; } = Guid.NewGuid().ToString();
	[JsonProperty("device_index")]
	public int DeviceIndex { get; set; } = 0;
	[JsonProperty("device_serial_number")]
	public string DeviceSerialNumber { get; set; } = "";
	[JsonProperty("haptic_strength")]
	public float HapticStrength { get; set; } = 0.5f;
	[JsonProperty("haptic_trigger_params")]
	public ObservableCollection<HapticTriggerQueryableParameter> HapticTriggerParams { get; set; } = [];

	public bool Equals(HapticTrigger? other)
	{
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;

		return DeviceIndex.Equals(other.DeviceIndex) && HapticStrength.Equals(other.HapticStrength) && HapticTriggerParams.SequenceEqual(other.HapticTriggerParams);
	}
}

public class HapticTriggerQueryableParameter : QueryableParameter
{
}
