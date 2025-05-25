using VRCOSC.App.SDK.Modules;

namespace FuviiOSC.SqueakMeter;

[ModuleTitle("Squeak Meter")]
[ModuleDescription("Listens to the default audio output device and provides OSC parameters for volume, frequencies and direction")]
[ModuleType(ModuleType.Generic)]
public class SqueakMeterModule : Module
{
    private float _bass;
    private float _mid;
    private float _treble;
    private float _leftEarVolume;
    private float _rightEarVolume;
    private float _direction = 0;
    private float _volume = 0;

    protected override void OnPreLoad()
    {
    }
}
