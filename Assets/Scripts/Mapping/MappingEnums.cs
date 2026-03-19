// Assets/Scripts/Mapping/MappingEnums.cs
using UnityEngine;

namespace MappingTool
{
    public enum MappingCondition
    {
        DiminishedSelf,
        NaiveLLM,
        Expert,
        Custom
    }

    public enum OutputParam
        {
            NebulaCompression, 
            StarColorHue, 
            DustOscAmplitude   
        }

    public enum InputSignal
    {
        HandsHorizontalDistance01,
        RightWristPronation01,
        //TorsoRespFreq01, 
        HeadPitchDeg, 
        LeftArmAngle01,
        RightArmAngle01,

        H10BreathAmp01,

        [DebugOnly] H10BreathAmpRaw01,
        [DebugOnly] H10BreathWave01,

        [DebugOnly] HandsHorizontalDistanceM, 
        [DebugOnly] RightWristPronationDeg,
        //[DebugOnly] TorsoRespFreqHz,   
        [DebugOnly] LeftArmAngleDeg,  
        [DebugOnly] RightArmAngleDeg,
        
        [DebugOnly] TorsoRespFreq01,
        // TorsoRespFreq01,  
        TorsoRespAmplitude01, 

        [DebugOnly] TorsoRespFreqHz, 
        [DebugOnly] TorsoRespPeriodSec,
        [DebugOnly] ChestLocalY,   
        [DebugOnly] ChestRelativeY, 

        StarFeetDistance01,
        [DebugOnly] StarFeetDistanceM,

        [DebugOnly] LeftArmRaise01,
        [DebugOnly] RightArmRaise01,
        ArmsRaise01,

        ArduinoPressure01,
        [DebugOnly] ArduinoPressureRaw
    }
}
