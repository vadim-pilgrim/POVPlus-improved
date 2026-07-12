using System.Numerics;

namespace SamplePlugin
{
    /// <summary>
    /// Live per-frame runtime state shared across the plugin. Persisted user settings live in
    /// <see cref="Configuration"/>, not here.
    /// </summary>
    internal static class GlobalVars
    {
        // Guards double-firing of Initialize().
        public static float InitOnlyOncePls = 0;

        // Bone-picker state (X = yaw source, Z = pitch source) surfaced in the experimental UI.
        public static int RotationBoneValueX = 46;
        public static string RotationBoneValueXName = "ERROR";
        public static int RotationBoneValueZ = 46;
        public static string RotationBoneValueZName = "ERROR";
        public static int PlayerBoneCount = 1;

        // Name of the bone the camera position is currently pinned to (for the UI picker).
        public static string BoneToBindName = "ERROR";

        // Head-bone rotation, read fresh each frame.
        public static Quaternion CameraQuartCurrent;

        // Character (body) yaw tracking.
        public static float PlayerXRotationCurrent;
        public static float PlayerXRotationPrev;
        public static float PlayerXRotationNormalize;

        // Head-bone yaw/pitch tracking (current vs. previous frame).
        public static float BoneYawRotateCurrent = 0;
        public static float BoneYawRotateBefore = 0;
        public static float BonePitchRotateCurrent = 0;
        public static float BonePitchRotateBefore = 0;

        public static float TotalRotationOffsetAddition = 0;
        public static float TOTALYAW = 0;

        // Movement / right-click-drag detection (experimental yaw binding).
        public static Vector3 LastPlayerPosition;
        public static Vector3 CurrentPlayerPosition;
        public static bool RightClickMoving;
        public static bool StationaryRotateRight;
        public static float TemporalYawPrevious = 10;

        // Vanilla camera values captured at init, restored when the mod stands down.
        public static float PreviousMaxVRotation;
        public static float PreviousMinVRotation;
        public static float PreviousCurrentFoV;
        public static float PreviousTilt;
        public static float PreviousMinFOV;

        // When false, the body is allowed to be culled again (mod disabled / third person).
        public static bool HideOwnBody = true;
    }
}
