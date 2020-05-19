using RPR4U.RPRUnityEditor.Data;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace RPR4U.RPRUnityEditor
{
    public class RenderEditorWindow : EditorWindow
    {
        private Rect adaptativeSamplingButtonRect;
        private Rect lightMultipliergButtonRect;
        private Texture2D renderTexture;
        private SceneRender sceneRender;
        private SceneSettings sceneSettings;
        private EditorCoroutine updateCoroutine;

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

        [MenuItem("Extensions/RPR4U/Render viewport")]
        public static void ShowWindow()
        {
            var w = (RenderEditorWindow)GetWindow(typeof(RenderEditorWindow), false, "Render View");

            w.sceneSettings = new SceneSettings
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
        }

        protected void OnDestroy()
        {
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

        private void DrawMenu_AdaptativeSampling()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Adaptative Sampling"), FocusType.Passive))
            {
                PopupWindow.Show(this.adaptativeSamplingButtonRect, new AdaptativePopUpMenu(this.sceneSettings));
            }

            if (Event.current.type == EventType.Repaint)
            {
                this.adaptativeSamplingButtonRect = GUILayoutUtility.GetLastRect();
            }
        }

        private void DrawMenu_LightMultiplier()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Light Multiplier"), FocusType.Passive))
            {
                PopupWindow.Show(this.lightMultipliergButtonRect, new LightPopUpMenu(this.sceneSettings));
            }

            if (Event.current.type == EventType.Repaint)
            {
                this.lightMultipliergButtonRect = GUILayoutUtility.GetLastRect();
            }
        }

        private void DrawMenu_Lower()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
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

            GUILayout.FlexibleSpace();

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            GUILayout.Label($"Versión: {version}");
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

        private void DrawMenu_Save()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("File"), FocusType.Passive))
            {
                // create the menu and add items to it
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
                    AboutWindow.Open();
                });

                menu.ShowAsContext();
            }
        }

        private void DrawMenu_Upper()
        {
            var unityCameras = (from t in Camera.allCameras select t).ToArray();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUIUtility.labelWidth = 40;
            EditorGUIUtility.fieldWidth = 120;
            this.sceneSettings.Render.Mode = (RadeonProRender.RenderMode)EditorGUILayout.EnumPopup("Mode:", this.sceneSettings.Render.Mode);

            EditorGUIUtility.labelWidth = 50;
            EditorGUIUtility.fieldWidth = 150;
            this.sceneSettings.Camera.Mode = (RadeonProRender.CameraMode)EditorGUILayout.EnumPopup("Camera:", this.sceneSettings.Camera.Mode);

            if (this.sceneSettings.Camera.Mode == RadeonProRender.CameraMode.LatitudLongitudeStereo ||
                this.sceneSettings.Camera.Mode == RadeonProRender.CameraMode.CubemapStereo)
            {
                EditorGUIUtility.labelWidth = 25;
                EditorGUIUtility.fieldWidth = 40;

                this.sceneSettings.Camera.IPD = Mathf.Clamp(EditorGUILayout.FloatField("Ipd:", this.sceneSettings.Camera.IPD), 0, 100);
            }

            EditorGUIUtility.labelWidth = 95;
            EditorGUIUtility.fieldWidth = 120;

            this.sceneSettings.Camera.SelectedCamera = EditorGUILayout.Popup("Render Camera:", this.sceneSettings.Camera.SelectedCamera, (from t in unityCameras select t.name).ToArray());

            EditorGUIUtility.labelWidth = 15;
            EditorGUIUtility.fieldWidth = 45;
            this.sceneSettings.Render.ImageWidth = Mathf.Clamp(EditorGUILayout.IntField("W:", this.sceneSettings.Render.ImageWidth), 0, 8192);

            switch (this.sceneSettings.Camera.Mode)
            {
                case RadeonProRender.CameraMode.CubeMap:
                case RadeonProRender.CameraMode.LatitudLongitude360:
                    this.sceneSettings.Render.ImageHeight = this.sceneSettings.Render.ImageWidth / 2;
                    break;

                case RadeonProRender.CameraMode.CubemapStereo:
                case RadeonProRender.CameraMode.FishEye:
                case RadeonProRender.CameraMode.LatitudLongitudeStereo:
                    this.sceneSettings.Render.ImageHeight = this.sceneSettings.Render.ImageWidth;
                    break;

                default:
                    EditorGUIUtility.labelWidth = 15;
                    EditorGUIUtility.fieldWidth = 45;
                    this.sceneSettings.Render.ImageHeight = Mathf.Clamp(EditorGUILayout.IntField("H:", this.sceneSettings.Render.ImageHeight), 0, 8192);
                    break;
            }

            EditorGUIUtility.labelWidth = 75;
            EditorGUIUtility.fieldWidth = 45;
            this.sceneSettings.Render.NumIterations = Mathf.Clamp(EditorGUILayout.IntField("Max Iterations:", this.sceneSettings.Render.NumIterations), 0, 99999);

            this.DrawMenu_AdaptativeSampling();
            this.DrawMenu_LightMultiplier();

            GUILayout.FlexibleSpace();

            this.DrawMenu_Render();
            this.DrawMenu_Save();

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
            this.SceneRender.Initialize(
                      RadeonProRender.CreationFlags.Gpu00 | RadeonProRender.CreationFlags.Gpu01,
                      this.sceneSettings);
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

        private class AboutWindow : EditorWindow
        {
            public static void Open()
            {
                var w = ScriptableObject.CreateInstance<AboutWindow>();
                w.position = new Rect(Screen.width / 2, Screen.height / 2, 250, 150);
                w.ShowPopup();
            }

            protected void OnGUI()
            {
                EditorGUILayout.LabelField("This is an example of EditorWindow.ShowPopup", EditorStyles.wordWrappedLabel);
                GUILayout.Space(70);
                if (GUILayout.Button("Agree!")) this.Close();
            }
        }

        private class AdaptativePopUpMenu : PopupWindowContent
        {
            private readonly SceneSettings sceneSettings;

            public AdaptativePopUpMenu(SceneSettings sceneSettings)
            {
                this.sceneSettings = sceneSettings;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(200, 100);
            }

            public override void OnGUI(Rect rect)
            {
                this.sceneSettings.Adaptative.Enabled = EditorGUILayout.Toggle("Enabled:", this.sceneSettings.Adaptative.Enabled);

                if (this.sceneSettings.Adaptative.Enabled)
                {
                    this.sceneSettings.Adaptative.MinSamples = Mathf.Clamp(EditorGUILayout.IntField("Minimum samples:", this.sceneSettings.Adaptative.MinSamples), 10, 99999);
                    this.sceneSettings.Adaptative.TileSize = Mathf.Clamp(EditorGUILayout.IntField("Tile Size:", this.sceneSettings.Adaptative.TileSize), 4, 32);
                    this.sceneSettings.Adaptative.Threshold = Mathf.Clamp(EditorGUILayout.FloatField("Tolerance:", this.sceneSettings.Adaptative.Threshold), 0f, 1f);
                }
            }
        }

        private class LightPopUpMenu : PopupWindowContent
        {
            private readonly SceneSettings sceneSettings;

            public LightPopUpMenu(SceneSettings sceneSettings)
            {
                this.sceneSettings = sceneSettings;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(200, 100);
            }

            public override void OnGUI(Rect rect)
            {
                this.sceneSettings.Light.DirectionalLightMultiplier = Mathf.Clamp(EditorGUILayout.FloatField("Directional Light:", this.sceneSettings.Light.DirectionalLightMultiplier), 0, 10);
                this.sceneSettings.Light.PointLightMultiplier = Mathf.Clamp(EditorGUILayout.FloatField("Point Light:", this.sceneSettings.Light.PointLightMultiplier), 0, 10);
                this.sceneSettings.Light.SpotLightMultiplier = Mathf.Clamp(EditorGUILayout.FloatField("Spot Light:", this.sceneSettings.Light.SpotLightMultiplier), 0, 10);
            }
        }
    }
}