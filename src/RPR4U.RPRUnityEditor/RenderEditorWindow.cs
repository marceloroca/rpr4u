using Newtonsoft.Json;
using RPR4U.RPRUnityEditor.Data;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace RPR4U.RPRUnityEditor
{
    public class RenderparentWindow : EditorWindow
    {
        private Rect lightMultipliergButtonRect;
        private Texture2D renderTexture;
        private SceneRender sceneRender;
        private SceneSettings settings;
        private EditorCoroutine updateCoroutine;
        private int zoomLevel;

        private SceneRender SceneRender
        {
            get
            {
                if (this.sceneRender == null)
                {
                    this.sceneRender = new SceneRender(Path.Combine(Application.streamingAssetsPath, "RPR4U", "ProRender"));
                }

                return this.sceneRender;
            }
        }

        private SceneSettings Settings
        {
            get
            {
                if (this.settings == null)
                {
                    var json = EditorPrefs.GetString("rpr4u.viewport.settings", string.Empty);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        this.settings = new SceneSettings
                        {
                            Render = new SceneSettings.RenderSettings
                            {
                                Mode = RadeonProRender.RenderMode.GlobalIllumination,
                                ImageWidth = 1024,
                                ImageHeight = 512,
                                NumIterations = 100,
                            },
                            Camera = new SceneSettings.CameraSettings
                            {
                                Mode = RadeonProRender.CameraMode.Perspective,
                                IPD = .65f,
                                SelectedCamera = 0,
                            },
                            Adaptative = new SceneSettings.AdaptativeSettings
                            {
                                Enabled = false,
                                MinSamples = 100,
                                Threshold = .05f,
                                TileSize = 16,
                            },
                            Light = new SceneSettings.LightSettings
                            {
                                DirectionalLightMultiplier = 6,
                                PointLightMultiplier = 2,
                                SpotLightMultiplier = 2,
                            },
                        };

                        this.SaveSettings();
                    }
                    else
                    {
                        this.settings = JsonConvert.DeserializeObject<SceneSettings>(json);
                    }
                }

                return this.settings;
            }
        }

        [MenuItem("Extensions/RPR4U/Render viewport")]
        public static void ShowWindow()
        {
            GetWindow(typeof(RenderparentWindow), false, "Render View");
        }

        protected void OnDestroy()
        {
            this.SaveSettings();

            if (this.updateCoroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(this.updateCoroutine);
            }

            if (this.sceneRender == null)
            {
                return;
            }

            this.sceneRender.StopRender();
            this.sceneRender.Dispose();
        }

        protected void OnGUI()
        {
            this.DrawTexturePreview();

            EditorGUILayout.BeginVertical();
            this.DrawMenu_Upper();
            GUILayout.FlexibleSpace();
            this.DrawMenu_Lower();
            EditorGUILayout.EndVertical();
        }

        private void DrawMenu_File()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("File"), FocusType.Passive))
            {
                var menu = new GenericMenu();

                if (this.SceneRender.IsRendering)
                {
                    menu.AddDisabledItem(new GUIContent("Save image"));
                    menu.AddDisabledItem(new GUIContent("Export scene"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Save image"), false, () =>
                    {
                        var path = EditorUtility.SaveFilePanel("Save Image", "", "scene_export.png", "png");

                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            if (this.SceneRender.IterationCount > 0)
                            {
                                var pix = this.renderTexture.GetPixels();
                                System.Array.Reverse(pix, 0, pix.Length);

                                var rotatedTexture = new Texture2D(this.renderTexture.width, this.renderTexture.height, TextureFormat.RGBA32, false, true);
                                rotatedTexture.SetPixels(pix);

                                File.WriteAllBytes(path, rotatedTexture.EncodeToPNG());
                            }
                        }
                    });

                    menu.AddItem(new GUIContent("Export scene"), false, () =>
                    {
                        var path = EditorUtility.SaveFilePanel("Export Scene", "", "scene_export.rprs", "rprs");

                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            if (!this.SceneRender.IsInitialized)
                            {
                                this.InitializeScene();
                            }

                            this.SceneRender.ExportScene(path, true);
                        }
                    });
                }

                menu.AddSeparator(string.Empty);

                menu.AddItem(new GUIContent("About..."), false, () =>
                {
                });

                menu.ShowAsContext();
            }
        }

        private void DrawMenu_Lower()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUIUtility.labelWidth = 75;
            EditorGUIUtility.fieldWidth = 30;

            this.zoomLevel = EditorGUILayout.IntSlider("Zoom level: ", this.zoomLevel, 0, 100);

            GUILayout.FlexibleSpace();

            if (this.SceneRender.IsRendering)
            {
                GUILayout.Label($"Rendering ");
            }

            if (this.SceneRender.IterationCount > 0)
            {
                GUILayout.Label($"Iterations: {this.SceneRender.IterationCount}");
                GUILayout.Label($"Time: {this.SceneRender.RenderingTime:hh\\:mm\\.ss}");
                GUILayout.Label($"Speed: {(this.SceneRender.IterationCount / this.SceneRender.RenderingTime.TotalSeconds):#0.00}");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMenu_Render()
        {
            if (this.SceneRender.IsRendering)
            {
                if (GUILayout.Button("Stop"))
                {
                    this.SceneRender.StopRender();
                    EditorCoroutineUtility.StopCoroutine(this.updateCoroutine);
                }
            }
            else
            {
                if (GUILayout.Button("Render"))
                {
                    this.InitializeScene();
                    this.SceneRender.StartRender();
                    this.updateCoroutine = EditorCoroutineUtility.StartCoroutine(this.UpdateTexture(), this);
                }
            }
        }

        private void DrawMenu_Upper()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (EditorGUILayout.DropdownButton(new GUIContent("Settings"), FocusType.Passive))
            {
                PopupWindow.Show(this.lightMultipliergButtonRect, new ViewportPopUpMenu(this));
            }

            if (Event.current.type == EventType.Repaint)
            {
                this.lightMultipliergButtonRect = GUILayoutUtility.GetLastRect();
            }

            this.DrawMenu_File();
            this.DrawMenu_Render();

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Versión: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTexturePreview()
        {
            if (this.renderTexture != null)
            {
                var w = (this.position.width - 8) / this.renderTexture.width;
                var h = (this.position.height - 50) / this.renderTexture.height;

                var m = Mathf.Min(w, h);

                var w2 = this.renderTexture.width * m;
                var h2 = this.renderTexture.height * m;

                var x = 4f;
                var y = 25f;

                if (w > h)
                {
                    x += (this.position.width - w2 - 8) / 2f;
                }
                else if (h > w)
                {
                    y += (this.position.height - h2 - 50) / 2f;
                }

                var pivot = new Vector2(x + w2 / 2, y + h2 / 2);

                var matrixBackup = GUI.matrix;
                GUIUtility.RotateAroundPivot(180, pivot);
                EditorGUI.DrawPreviewTexture(new Rect(x, y, w2, h2), this.renderTexture);
                GUI.matrix = matrixBackup;
            }
        }

        private void InitializeScene()
        {
            this.SceneRender.Initialize(RadeonProRender.CreationFlags.Gpu00 | RadeonProRender.CreationFlags.Gpu01, this.Settings);
        }

        private void SaveSettings()
        {
            EditorPrefs.SetString("rpr4u.viewport.settings", JsonConvert.SerializeObject(this.Settings));
        }

        private IEnumerator UpdateTexture()
        {
            while (true)
            {
                if (this.sceneRender != null)
                {
                    this.renderTexture = this.SceneRender?.GetTexture();
                    this.Repaint();
                }

                yield return new EditorWaitForSeconds(.1f);
            }
        }

        private class ViewportPopUpMenu : PopupWindowContent
        {
            private readonly RenderparentWindow parentWindow;
            private Vector2 scrollPosition;

            private Vector2 windowSize;

            public ViewportPopUpMenu(RenderparentWindow parentWindow)
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

            private bool showRenderSettings
            {
                get { return EditorPrefs.GetBool("rpr4u.viewport.showRenderSettings"); }
                set { EditorPrefs.SetBool("rpr4u.viewport.showRenderSettings", value); }
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
                            break;

                        case RadeonProRender.CameraMode.CubemapStereo:
                        case RadeonProRender.CameraMode.FishEye:
                        case RadeonProRender.CameraMode.LatitudLongitudeStereo:
                            this.parentWindow.Settings.Render.ImageHeight = this.parentWindow.Settings.Render.ImageWidth;
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

                    --EditorGUI.indentLevel;
                }
            }
        }
    }
}