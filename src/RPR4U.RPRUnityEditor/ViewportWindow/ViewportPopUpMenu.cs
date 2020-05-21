using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RPR4U.RPRUnityEditor
{
    public partial class ViewportWindow : EditorWindow
    {
        private class ViewportPopUpMenu : PopupWindowContent
        {
            private readonly ViewportWindow parentWindow;
            private Vector2 scrollPosition;

            private Vector2 windowSize;

            public ViewportPopUpMenu(ViewportWindow parentWindow)
            {
                this.parentWindow = parentWindow;
                this.windowSize = new Vector2(350, this.parentWindow.position.height - 30);
            }

            private bool showAdaptativeSettings
            {
                get { return EditorPrefs.GetBool("rpr4u.viewport.showAdaptativeSettings"); }
                set { EditorPrefs.SetBool("rpr4u.viewport.showAdaptativeSettings", value); }
            }

            private bool showCameraSettings
            {
                get { return EditorPrefs.GetBool("rpr4u.viewport.showCameraSettings"); }
                set { EditorPrefs.SetBool("rpr4u.viewport.showCameraSettings", value); }
            }

            private bool showLightSettings
            {
                get { return EditorPrefs.GetBool("rpr4u.viewport.showLightSettings"); }
                set { EditorPrefs.SetBool("rpr4u.viewport.showLightSettings", value); }
            }

            private bool showRenderRayDepthSettings
            {
                get { return EditorPrefs.GetBool("rpr4u.viewport.showRenderrRayDepthSettings"); }
                set { EditorPrefs.SetBool("rpr4u.viewport.showRenderrRayDepthSettings", value); }
            }

            private bool showRenderSettings
            {
                get { return EditorPrefs.GetBool("rpr4u.viewport.showRenderSettings"); }
                set { EditorPrefs.SetBool("rpr4u.viewport.showRenderSettings", value); }
            }

            private bool showViewportSettings
            {
                get { return EditorPrefs.GetBool("rpr4u.viewport.showViewportSettings"); }
                set { EditorPrefs.SetBool("rpr4u.viewport.showViewportSettings", value); }
            }

            public override Vector2 GetWindowSize()
            {
                return this.windowSize;
            }

            public override void OnGUI(Rect rect)
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.BeginVertical();

                this.scrollPosition = EditorGUILayout.BeginScrollView(this.scrollPosition);

                this.DrawRenderSettings();
                this.DrawViewportSettings();
                this.DrawCameraSettings();
                this.DrawAdaptativeSettings();
                this.DrawLightSettings();

                EditorGUILayout.EndScrollView();

                EditorGUILayout.EndVertical();

                if (EditorGUI.EndChangeCheck())
                {
                    switch (this.parentWindow.Settings.Camera.Mode)
                    {
                        case RadeonProRender.CameraMode.CubeMap:
                        case RadeonProRender.CameraMode.LatitudLongitude360:
                            this.parentWindow.Settings.Render.ImageHeight = this.parentWindow.Settings.Render.ImageWidth / 2;
                            this.parentWindow.Settings.Viewport.ImageHeight = this.parentWindow.Settings.Viewport.ImageWidth / 2;
                            break;

                        case RadeonProRender.CameraMode.CubemapStereo:
                        case RadeonProRender.CameraMode.FishEye:
                        case RadeonProRender.CameraMode.LatitudLongitudeStereo:
                            this.parentWindow.Settings.Render.ImageHeight = this.parentWindow.Settings.Render.ImageWidth;
                            this.parentWindow.Settings.Viewport.ImageHeight = this.parentWindow.Settings.Viewport.ImageWidth;
                            break;
                    }

                    this.parentWindow.SaveSettings();
                }
            }

            private void DrawAdaptativeSettings()
            {
                this.showAdaptativeSettings = EditorGUILayout.Foldout(this.showAdaptativeSettings, "Adaptative Settings");

                if (this.showAdaptativeSettings)
                {
                    ++EditorGUI.indentLevel;

                    this.parentWindow.Settings.Adaptative.Enabled = EditorGUILayout.Toggle("Enabled:", this.parentWindow.Settings.Adaptative.Enabled);

                    if (this.parentWindow.Settings.Adaptative.Enabled)
                    {
                        this.parentWindow.Settings.Adaptative.MinSamples = Mathf.Clamp(EditorGUILayout.IntField("Minimum samples:", this.parentWindow.Settings.Adaptative.MinSamples), 10, 99999);
                        this.parentWindow.Settings.Adaptative.TileSize = Mathf.Clamp(EditorGUILayout.IntField("Tile Size:", this.parentWindow.Settings.Adaptative.TileSize), 4, 32);
                        this.parentWindow.Settings.Adaptative.Threshold = Mathf.Clamp(EditorGUILayout.FloatField("Tolerance:", this.parentWindow.Settings.Adaptative.Threshold), 0f, 1f);
                    }

                    --EditorGUI.indentLevel;
                }
            }

            private void DrawCameraSettings()
            {
                this.showCameraSettings = EditorGUILayout.Foldout(this.showCameraSettings, "Camera Settings");

                if (this.showCameraSettings)
                {
                    ++EditorGUI.indentLevel;

                    var unityCameras = (from t in Camera.allCameras select t).ToArray();
                    this.parentWindow.Settings.Camera.SelectedCamera = EditorGUILayout.Popup("Camera:", this.parentWindow.Settings.Camera.SelectedCamera, (from t in unityCameras select t.name).ToArray());

                    var cameraMode = (RadeonProRender.CameraMode)EditorGUILayout.EnumPopup("Mode:", this.parentWindow.Settings.Camera.Mode);

                    if (cameraMode != this.parentWindow.Settings.Camera.Mode)
                    {
                        this.parentWindow.Settings.Camera.Mode = cameraMode;
                    }

                    if (this.parentWindow.Settings.Camera.Mode == RadeonProRender.CameraMode.LatitudLongitudeStereo ||
                        this.parentWindow.Settings.Camera.Mode == RadeonProRender.CameraMode.CubemapStereo)
                    {
                        this.parentWindow.Settings.Camera.IPD = Mathf.Clamp(EditorGUILayout.FloatField("Ipd:", this.parentWindow.Settings.Camera.IPD), 0, 100);
                    }

                    --EditorGUI.indentLevel;
                }
            }

            private void DrawLightSettings()
            {
                this.showLightSettings = EditorGUILayout.Foldout(this.showLightSettings, "Light Settings");

                if (this.showLightSettings)
                {
                    ++EditorGUI.indentLevel;

                    this.parentWindow.Settings.Light.DirectionalLightMultiplier = Mathf.Clamp(EditorGUILayout.FloatField("Directional Light:", this.parentWindow.Settings.Light.DirectionalLightMultiplier), 0, 10);
                    this.parentWindow.Settings.Light.PointLightMultiplier = Mathf.Clamp(EditorGUILayout.FloatField("Point Light:", this.parentWindow.Settings.Light.PointLightMultiplier), 0, 10);
                    this.parentWindow.Settings.Light.SpotLightMultiplier = Mathf.Clamp(EditorGUILayout.FloatField("Spot Light:", this.parentWindow.Settings.Light.SpotLightMultiplier), 0, 10);

                    --EditorGUI.indentLevel;
                }
            }

            private void DrawRenderSettings()
            {
                this.showRenderSettings = EditorGUILayout.Foldout(this.showRenderSettings, "Render Settings");

                if (this.showRenderSettings)
                {
                    ++EditorGUI.indentLevel;

                    this.parentWindow.Settings.Render.Mode = (RadeonProRender.RenderMode)EditorGUILayout.EnumPopup("Mode:", this.parentWindow.Settings.Render.Mode);

                    this.parentWindow.Settings.Render.NumIterations = Mathf.Clamp(EditorGUILayout.IntField("Max Iterations:", this.parentWindow.Settings.Render.NumIterations), 0, 8192);

                    this.parentWindow.Settings.Render.ImageWidth = Mathf.Clamp(EditorGUILayout.IntField("Image Width:", this.parentWindow.Settings.Render.ImageWidth), 0, 8192);

                    if (this.parentWindow.Settings.Camera.Mode == RadeonProRender.CameraMode.Perspective || this.parentWindow.Settings.Camera.Mode == RadeonProRender.CameraMode.Orthographic)
                    {
                        this.parentWindow.Settings.Render.ImageHeight = Mathf.Clamp(EditorGUILayout.IntField("Image Height:", this.parentWindow.Settings.Render.ImageHeight), 0, 8192);
                    }

                    this.parentWindow.Settings.Render.RadianceClamp = this.FloatField("Radiance clamp", this.parentWindow.Settings.Render.RadianceClamp, 2, 0, 100);
                    this.parentWindow.Settings.Render.Recursion = this.IntField("Recursion", this.parentWindow.Settings.Render.Recursion, 10, 0, 100);

                    this.showRenderRayDepthSettings = EditorGUILayout.Foldout(this.showRenderRayDepthSettings, "Max Ray Depth");

                    if (this.showRenderRayDepthSettings)
                    {
                        ++EditorGUI.indentLevel;

                        if (this.parentWindow.Settings.Render.RayDepth == null)
                        {
                            this.parentWindow.Settings.Render.RayDepth = new Data.SceneSettings.RayDepthSettings();
                        }

                        this.parentWindow.Settings.Render.RayDepth.MaxDiffuse = this.IntField("Diffuse", this.parentWindow.Settings.Render.RayDepth.MaxDiffuse, 3, 0, 100);
                        this.parentWindow.Settings.Render.RayDepth.MaxGlossy = this.IntField("Glossy", this.parentWindow.Settings.Render.RayDepth.MaxGlossy, 5, 0, 100);
                        this.parentWindow.Settings.Render.RayDepth.MaxRefraction = this.IntField("Refraction", this.parentWindow.Settings.Render.RayDepth.MaxRefraction, 5, 0, 100);
                        this.parentWindow.Settings.Render.RayDepth.MaxGlossyRefraction = this.IntField("Glossy Refraction", this.parentWindow.Settings.Render.RayDepth.MaxGlossyRefraction, 5, 0, 100);
                        this.parentWindow.Settings.Render.RayDepth.MaxShadow = this.IntField("Shadow", this.parentWindow.Settings.Render.RayDepth.MaxShadow, 5, 0, 100);

                        --EditorGUI.indentLevel;
                    }

                    --EditorGUI.indentLevel;
                }
            }

            private void DrawViewportSettings()
            {
                this.showViewportSettings = EditorGUILayout.Foldout(this.showViewportSettings, "Render Settings - Viewport");

                if (this.showViewportSettings)
                {
                    ++EditorGUI.indentLevel;

                    this.parentWindow.Settings.Viewport.NumIterations = Mathf.Clamp(EditorGUILayout.IntField("Max Iterations:", this.parentWindow.Settings.Viewport.NumIterations), 0, 8192);

                    this.parentWindow.Settings.Viewport.ImageWidth = Mathf.Clamp(EditorGUILayout.IntField("Image Width:", this.parentWindow.Settings.Viewport.ImageWidth), 0, 8192);

                    if (this.parentWindow.Settings.Camera.Mode == RadeonProRender.CameraMode.Perspective || this.parentWindow.Settings.Camera.Mode == RadeonProRender.CameraMode.Orthographic)
                    {
                        this.parentWindow.Settings.Viewport.ImageHeight = Mathf.Clamp(EditorGUILayout.IntField("Image Height:", this.parentWindow.Settings.Viewport.ImageHeight), 0, 8192);
                    }

                    --EditorGUI.indentLevel;
                }
            }

            private float? FloatField(string title, float? value, float defaultValue, float min, float max)
            {
                var hasValue = value.HasValue;

                EditorGUILayout.BeginHorizontal();

                var labelWidth = EditorGUIUtility.labelWidth;

                EditorGUIUtility.labelWidth *= .5f;
                hasValue = EditorGUILayout.ToggleLeft(title, hasValue);

                if (!hasValue)
                {
                    value = null;
                }
                else if (!value.HasValue)
                {
                    value = defaultValue;
                }

                if (value.HasValue)
                {
                    EditorGUIUtility.labelWidth = 0;
                    value = Mathf.Clamp(EditorGUILayout.FloatField("", value.Value), min, max);
                }

                EditorGUIUtility.labelWidth = labelWidth;

                EditorGUILayout.EndHorizontal();

                return value;
            }

            private int? IntField(string title, int? value, int defaultValue, int min, int max)
            {
                var hasValue = value.HasValue;

                EditorGUILayout.BeginHorizontal();

                var labelWidth = EditorGUIUtility.labelWidth;

                EditorGUIUtility.labelWidth *= .5f;
                hasValue = EditorGUILayout.ToggleLeft(title, hasValue);

                if (!hasValue)
                {
                    value = null;
                }
                else if (!value.HasValue)
                {
                    value = defaultValue;
                }

                if (value.HasValue)
                {
                    EditorGUIUtility.labelWidth = 0;
                    value = Mathf.Clamp(EditorGUILayout.IntField("", value.Value), min, max);
                }

                EditorGUIUtility.labelWidth = labelWidth;

                EditorGUILayout.EndHorizontal();

                return value;
            }
        }
    }
}