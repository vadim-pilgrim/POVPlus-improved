using System;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Hypostasis.Game.Structures;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using static FFXIVClientStructs.Havok.Animation.Rig.hkaPose;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

using StructsObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace SamplePlugin
{
    /// <summary>
    /// Core of the plugin. Hooks the world camera's GetCameraPosition and, while enabled in first
    /// person, moves the camera onto the player's head bone and (optionally) drives its yaw/pitch
    /// from the head bone's animated rotation so emotes/animations feel immersive.
    /// </summary>
    public static unsafe class HideBody
    {
        // Player position from the previous frame, used to keep the bone-relative camera position stable.
        private static Vector3 PrevCameraTargetPosition;

        private static void GetCameraPositionDetour(GameCamera* camera, GameObject* target, Vector3* position, Bool swapPerson)
        {
            // Mod disabled entirely: restore vanilla camera and stop overriding body culling.
            if (!Plugin.P.Configuration.Setting_ModEnabled)
            {
                GlobalVars.DebugStatus = "mod disabled in settings";
                camera->VTable.getCameraPosition.Original(camera, target, position, swapPerson);
                DisabledMod();
                Plugin.P.DisableDrawGameObject();
                return;
            }

            // In third person while the mod is first-person-only: run vanilla and stand down.
            // camera->mode == 1 is third person.
            if (camera->mode == 1 && Plugin.P.Configuration.Setting_FirstPersonOnly)
            {
                GlobalVars.DebugStatus = $"THIRD PERSON (mode={camera->mode}) - zoom fully in to first person";
                camera->VTable.getCameraPosition.Original(camera, target, position, swapPerson);
                DisableModInThirdPerson();
                Plugin.P.DisableDrawGameObject();
                return;
            }

            GlobalVars.DebugStatus = $"FP path (mode={camera->mode})";

            // --- Camera position: pin it to the head bone plus the configured offset ---------------

            // Keep the chosen bone index non-negative. Only clamp the upper bound once we've
            // actually read a real skeleton (PlayerBoneCount > 1), otherwise the picker would
            // freeze at 1 before the skeleton is known.
            if (Plugin.P.Configuration.BoneToBind < 0)
                Plugin.P.Configuration.BoneToBind = 0;
            if (GlobalVars.PlayerBoneCount > 1 && Plugin.P.Configuration.BoneToBind > GlobalVars.PlayerBoneCount)
                Plugin.P.Configuration.BoneToBind = GlobalVars.PlayerBoneCount;

            var newPos = Common.GetBoneWorldPosition(target, (uint)Plugin.P.Configuration.BoneToBind)
                         + ((Vector3)target->Position - PrevCameraTargetPosition);

            float playerYaw = target->Rotation;

            Plugin.P.EnableDrawGameObject();

            // Rotate the configured X/Z offset by the player's facing so it stays head-relative.
            float cosY = MathF.Cos(playerYaw);
            float sinY = MathF.Sin(playerYaw);

            Vector3 rotatedOffset = new Vector3(
                Plugin.P.Configuration.OffsetX * sinY + Plugin.P.Configuration.OffsetZ * cosY,
                Plugin.P.Configuration.OffsetY,
                Plugin.P.Configuration.OffsetX * cosY - Plugin.P.Configuration.OffsetZ * sinY
            );

            newPos = rotatedOffset + newPos;

            camera->minFoV = Plugin.P.Configuration.Setting_FOV;

            // --- Camera rotation: driven from the head bone's animated pose ------------------------

            var player = Service.Objects.LocalPlayer;
            if (player == null)
            {
                GlobalVars.DebugStatus += " | player=NULL";
            }
            else
            {
                var playerBase = GetCharacterBase(player);
                if (playerBase == null)
                    GlobalVars.DebugStatus += " | charBase=NULL";
                else if (playerBase->Skeleton == null)
                    GlobalVars.DebugStatus += " | skeleton=NULL";
                else
                {
                    var skeleton = *playerBase->Skeleton;
                    var partialSkeleton = skeleton.PartialSkeletons;

                    // Partial skeleton 0 is the character body skeleton.
                    const int havokPoseVal = 0;

                    if (partialSkeleton == null)
                        GlobalVars.DebugStatus += " | partialSkeleton=NULL";
                    else if (partialSkeleton->GetHavokPose(havokPoseVal) == null)
                        GlobalVars.DebugStatus += " | havokPose=NULL";
                    else
                    {
                        var pose = partialSkeleton->GetHavokPose(havokPoseVal);
                        var boneArrayLen = pose->Skeleton->Bones.Length;
                        GlobalVars.DebugStatus += $" | bones={boneArrayLen}";

                        // Expose bone names / count for the (experimental) bone-picker UI.
                        GlobalVars.RotationBoneValueXName = pose->Skeleton->Bones[GlobalVars.RotationBoneValueX].Name.String;
                        GlobalVars.RotationBoneValueZName = pose->Skeleton->Bones[GlobalVars.RotationBoneValueZ].Name.String;
                        GlobalVars.PlayerBoneCount = boneArrayLen - 1; // -1 because Length is 1-based

                        // Name of the bone the camera is pinned to, for the UI picker.
                        if (Plugin.P.Configuration.BoneToBind >= 0 && Plugin.P.Configuration.BoneToBind < boneArrayLen)
                            GlobalVars.BoneToBindName = pose->Skeleton->Bones[Plugin.P.Configuration.BoneToBind].Name.String;

                        // --- Character yaw delta (no bone involved) ---
                        GlobalVars.PlayerXRotationCurrent = target->Rotation;
                        GlobalVars.PlayerXRotationNormalize = GlobalVars.PlayerXRotationCurrent - GlobalVars.PlayerXRotationPrev;
                        GlobalVars.PlayerXRotationPrev = GlobalVars.PlayerXRotationCurrent;

                        // --- Read the head bone's model-space rotation as a quaternion ---
                        var boneRotSource = pose->AccessBoneModelSpace(GlobalVars.RotationBoneValueX, PropagateOrNot.DontPropagate);
                        GlobalVars.CameraQuartCurrent = new Quaternion(
                            boneRotSource->Rotation.X, boneRotSource->Rotation.Y,
                            boneRotSource->Rotation.Z, boneRotSource->Rotation.W);
                        var normalisedQuart = Quaternion.Normalize(GlobalVars.CameraQuartCurrent);
                        Vector3 forward = Vector3.Transform(Vector3.UnitZ, normalisedQuart);

                        // Bone yaw (side to side) delta this frame.
                        GlobalVars.BoneYawRotateCurrent = MathF.Atan2(forward.X, forward.Z);
                        var boneYawRotateOffset = GlobalVars.BoneYawRotateCurrent - GlobalVars.BoneYawRotateBefore;
                        GlobalVars.BoneYawRotateBefore = GlobalVars.BoneYawRotateCurrent;

                        // Bone pitch (up/down): derived from a roll-style atan2 on the quaternion.
                        var v2Roll = MathF.Atan2(
                            2.0f * (normalisedQuart.X * normalisedQuart.Y + normalisedQuart.Z * normalisedQuart.W),
                            1.0f - 2.0f * (normalisedQuart.X * normalisedQuart.X + normalisedQuart.Z * normalisedQuart.Z));

                        GlobalVars.BonePitchRotateCurrent = -v2Roll + MathF.PI;
                        var bonePitchRotateOffset = GlobalVars.BonePitchRotateCurrent - GlobalVars.BonePitchRotateBefore;
                        GlobalVars.BonePitchRotateBefore = GlobalVars.BonePitchRotateCurrent;

                        // --- YAW (left/right) restriction bookkeeping -------------------------------
                        // Experimental: detects whether the player is turning via right-mouse-drag,
                        // WASD, or stationary turn. Currently only tracks state (no camera effect yet)
                        // and is the scaffold for fixing the right-click rotation glitch.
                        if (Plugin.P.Configuration.RotationBindBoolX)
                        {
                            var isMoving = false;

                            GlobalVars.CurrentPlayerPosition = target->Position;
                            if (GlobalVars.CurrentPlayerPosition != GlobalVars.LastPlayerPosition)
                                isMoving = true;
                            GlobalVars.LastPlayerPosition = target->Position;

                            // Moving while camera yaw matches facing => right-mouse-drag movement.
                            GlobalVars.RightClickMoving = isMoving && camera->currentHRotation == target->Rotation;

                            GlobalVars.TOTALYAW += boneYawRotateOffset;
                            GlobalVars.TotalRotationOffsetAddition -= boneYawRotateOffset;

                            if (!GlobalVars.RightClickMoving)
                            {
                                if (GlobalVars.TemporalYawPrevious == target->Rotation && isMoving)
                                {
                                    // Temporal fix: last frame's camera yaw already matches facing.
                                    GlobalVars.RightClickMoving = true;
                                }

                                GlobalVars.StationaryRotateRight =
                                    GlobalVars.TemporalYawPrevious == target->Rotation && !isMoving;

                                GlobalVars.TemporalYawPrevious = camera->currentHRotation;
                            }
                        }

                        // Apply the accumulated yaw to the camera.
                        float finalXrotation = 0f;
                        if (Plugin.P.Configuration.Setting_RotateWithplayer)
                            finalXrotation += GlobalVars.PlayerXRotationNormalize;
                        if (Plugin.P.Configuration.RotationBindBoolX)
                            finalXrotation += boneYawRotateOffset;
                        camera->currentHRotation += finalXrotation;

                        // --- PITCH (up/down) --------------------------------------------------------
                        if (!Plugin.P.Configuration.RotationBindBoolZ)
                        {
                            Common.CameraManager->worldCamera->maxVRotation = GlobalVars.PreviousMaxVRotation;
                            Common.CameraManager->worldCamera->minVRotation = GlobalVars.PreviousMinVRotation;
                            Common.CameraManager->worldCamera->tilt = GlobalVars.PreviousTilt;
                        }
                        else
                        {
                            var testPitch = camera->currentVRotation + bonePitchRotateOffset;

                            // Allow full vertical rotation (backflips etc.). Vanilla clamps ~[-85,45] deg.
                            camera->minVRotation = -90;
                            camera->maxVRotation = 90;

                            // Flip tilt by 180 deg when the head passes "upside down" so the view stays upright.
                            var inversion = (testPitch * (180f / Math.PI)) + 90;
                            var upright = (Math.Floor(inversion / 180f) % 2 == 0) ? 1 : 0;
                            camera->tilt = upright == 0 ? 3.14159f : 0;

                            camera->currentVRotation += bonePitchRotateOffset;
                        }
                    }
                }
            }

            // Store this frame's player position and commit the camera position.
            PrevCameraTargetPosition = target->Position;
            *position = newPos;
        }

        public static void Initialize()
        {
            // We only need a live world camera to hook. The old Hypostasis IsValid() checks
            // reflect-and-verify a bunch of unrelated game-function signatures and fail on newer
            // patches, which used to prevent the camera hook from ever being created.
            if (Common.CameraManager == null || Common.CameraManager->worldCamera == null)
            {
                GlobalVars.DebugStatus = "Init failed: CameraManager/worldCamera not ready";
                Service.Log.Warning("POV+ Initialize: CameraManager/worldCamera not ready, hook not created");
                return;
            }

            var worldCamera = Common.CameraManager->worldCamera;

            worldCamera->VTable.getCameraPosition.CreateHook(GetCameraPositionDetour);

            GlobalVars.PreviousMaxVRotation = worldCamera->maxVRotation;
            GlobalVars.PreviousMinVRotation = worldCamera->minVRotation;
            GlobalVars.PreviousCurrentFoV = worldCamera->currentFoV;
            GlobalVars.PreviousTilt = worldCamera->tilt;
            GlobalVars.PreviousMinFOV = worldCamera->minFoV;

            var hooked = worldCamera->VTable.getCameraPosition.IsHooked;
            GlobalVars.DebugStatus = $"Init OK: camera hook created (hooked={hooked})";
            Service.Log.Information($"POV+ Initialize: camera hook created, hooked={hooked}");
        }

        public static void Dispose()
        {
            // Must never throw: a throwing Dispose shows up as an "(unload error)" in Dalamud and
            // leaves the plugin wedged until the game restarts.
            try
            {
                if (Common.CameraManager != null && Common.CameraManager->worldCamera != null)
                {
                    var cam = Common.CameraManager->worldCamera;

                    var hook = cam->VTable.getCameraPosition.Hook;
                    hook?.Disable();
                    hook?.Dispose();

                    // Revert everything to the pre-plugin state.
                    cam->currentVRotation = 0;
                    cam->maxVRotation = GlobalVars.PreviousMaxVRotation;
                    cam->minVRotation = GlobalVars.PreviousMinVRotation;
                    cam->currentFoV = GlobalVars.PreviousCurrentFoV;
                    cam->tilt = GlobalVars.PreviousTilt;
                    cam->minFoV = GlobalVars.PreviousMinFOV;
                }
            }
            catch (Exception ex)
            {
                Service.Log.Error(ex, "Error during HideBody.Dispose (ignored so unload stays clean)");
            }

            Service.Log.Information($"===CLOSING===");
        }

        public static void DisableModInThirdPerson()
        {
            Common.CameraManager->worldCamera->maxVRotation = GlobalVars.PreviousMaxVRotation;
            Common.CameraManager->worldCamera->minVRotation = GlobalVars.PreviousMinVRotation;
            // NOTE: restoring currentFoV here breaks zoom-in, so it's intentionally left out.
            Common.CameraManager->worldCamera->tilt = GlobalVars.PreviousTilt;
            Common.CameraManager->worldCamera->minFoV = GlobalVars.PreviousMinFOV;
            GlobalVars.HideOwnBody = false;
        }

        public static void DisabledMod()
        {
            Common.CameraManager->worldCamera->maxVRotation = GlobalVars.PreviousMaxVRotation;
            Common.CameraManager->worldCamera->minVRotation = GlobalVars.PreviousMinVRotation;
            Common.CameraManager->worldCamera->tilt = GlobalVars.PreviousTilt;
            Common.CameraManager->worldCamera->minFoV = GlobalVars.PreviousMinFOV;
            GlobalVars.HideOwnBody = false;
        }

        public static unsafe T* GetDrawObject<T>(this IGameObject go) where T : unmanaged
            => (T*)((StructsObject*)go.Address)->DrawObject;

        public static unsafe CharacterBase* GetCharacterBase(this ICharacter go) => go.GetDrawObject<CharacterBase>();
    }
}
