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
        private int adaptativeMinSamples = 100;
        private Rect adaptativeSamplingButtonRect;
        private float adaptativeThreshold = 0.05f;
        private int adaptativeTileSize = 16;
        private float cameraIPD = 6.4f;
        private RadeonProRender.CameraMode cameraMode = RadeonProRender.CameraMode.Perspective;
        private int imageHeight = 512;
        private int imageWidth = 1024;
        private int numIterations = 5000;
        private RadeonProRender.RenderMode renderMode = RadeonProRender.RenderMode.GlobalIllumination;
        private Texture2D renderTexture;
        private SceneRender sceneRender;
        private int selectedCamera;
        private Camera unityCamera;
        private EditorCoroutine updateCoroutine;
        private float direcionalLigthMultiplier = 6;

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
            GetWindow(typeof(RenderEditorWindow), false, "Render View");
        }

        protected void OnDestroy()
        {
            EditorCoroutineUtility.StopCoroutine(this.updateCoroutine);

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

        private void DrawMenu_LightMultiplier()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Light Multiplier"), FocusType.Passive))
            {
                PopupWindow.Show(adaptativeSamplingButtonRect, new LightPopUpMenu(this));
            }

            if (Event.current.type == EventType.Repaint)
            {
                this.adaptativeSamplingButtonRect = GUILayoutUtility.GetLastRect();
            }
        }

        private void DrawMenu_AdaptativeSampling()
        {
            if (EditorGUILayout.DropdownButton(new GUIContent("Adaptative Sampling"), FocusType.Passive))
            {
                PopupWindow.Show(adaptativeSamplingButtonRect, new AdaptativePopUpMenu(this));
            }

            if (Event.current.type == EventType.Repaint)
            {
                this.adaptativeSamplingButtonRect = GUILayoutUtility.GetLastRect();
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

            GUILayout.Label($"Versión: 1.0.001(B)");
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
                                //this.SceneRender.SaveRender(path);

                                var pix = this.renderTexture.GetPixels();
                                System.Array.Reverse(pix, 0, pix.Length);

                                var rotatedTexture = new Texture2D(this.renderTexture.width, this.renderTexture.height);
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
            this.renderMode = (RadeonProRender.RenderMode)EditorGUILayout.EnumPopup("Mode:", this.renderMode);

            EditorGUIUtility.labelWidth = 50;
            EditorGUIUtility.fieldWidth = 150;
            this.cameraMode = (RadeonProRender.CameraMode)EditorGUILayout.EnumPopup("Camera:", this.cameraMode);

            if (this.cameraMode == RadeonProRender.CameraMode.LatitudLongitudeStereo ||
                this.cameraMode == RadeonProRender.CameraMode.CubemapStereo)
            {
                EditorGUIUtility.labelWidth = 25;
                EditorGUIUtility.fieldWidth = 40;

                this.cameraIPD = Mathf.Clamp(EditorGUILayout.FloatField("Ipd:", this.cameraIPD), 0, 100);
            }

            EditorGUIUtility.labelWidth = 95;
            EditorGUIUtility.fieldWidth = 120;

            this.selectedCamera = EditorGUILayout.Popup("Render Camera:", this.selectedCamera, (from t in unityCameras select t.name).ToArray());
            this.unityCamera = unityCameras[this.selectedCamera];

            EditorGUIUtility.labelWidth = 15;
            EditorGUIUtility.fieldWidth = 45;
            this.imageWidth = Mathf.Clamp(EditorGUILayout.IntField("W:", this.imageWidth), 0, 8192);

            switch (this.cameraMode)
            {
                case RadeonProRender.CameraMode.CubeMap:
                case RadeonProRender.CameraMode.LatitudLongitude360:
                    this.imageHeight = this.imageWidth / 2;
                    break;

                case RadeonProRender.CameraMode.CubemapStereo:
                case RadeonProRender.CameraMode.FishEye:
                case RadeonProRender.CameraMode.LatitudLongitudeStereo:
                    this.imageHeight = this.imageWidth;
                    break;

                default:
                    EditorGUIUtility.labelWidth = 15;
                    EditorGUIUtility.fieldWidth = 45;
                    this.imageHeight = Mathf.Clamp(EditorGUILayout.IntField("H:", this.imageHeight), 0, 8192);
                    break;
            }

            EditorGUIUtility.labelWidth = 75;
            EditorGUIUtility.fieldWidth = 45;
            this.numIterations = Mathf.Clamp(EditorGUILayout.IntField("Max Iterations:", this.numIterations), 0, 99999);

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
                      this.renderMode,
                      this.cameraMode,
                      this.unityCamera.GetInstanceID(),
                      this.cameraIPD,
                      this.imageWidth,
                      this.imageHeight,
                      this.numIterations,
                      this.adaptativeMinSamples,
                      this.adaptativeTileSize,
                      this.adaptativeThreshold,
                      this.direcionalLigthMultiplier);
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

        private class LightPopUpMenu : PopupWindowContent
        {
            private readonly RenderEditorWindow editor;

            public LightPopUpMenu(RenderEditorWindow editor)
            {
                this.editor = editor;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(200, 100);
            }

            public override void OnGUI(Rect rect)
            {
                this.editor.direcionalLigthMultiplier = Mathf.Clamp(EditorGUILayout.FloatField("Directional Light:", this.editor.direcionalLigthMultiplier), 0, 10);
            }
        }

        private class AdaptativePopUpMenu : PopupWindowContent
        {
            private readonly RenderEditorWindow editor;

            public AdaptativePopUpMenu(RenderEditorWindow editor)
            {
                this.editor = editor;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(200, 100);
            }

            public override void OnGUI(Rect rect)
            {
                this.editor.adaptativeMinSamples = Mathf.Clamp(EditorGUILayout.IntField("Minimum samples:", this.editor.adaptativeMinSamples), 10, 99999);
                this.editor.adaptativeTileSize = Mathf.Clamp(EditorGUILayout.IntField("Tile Size:", this.editor.adaptativeTileSize), 4, 32);
                this.editor.adaptativeThreshold = Mathf.Clamp(EditorGUILayout.FloatField("Tolerance:", this.editor.adaptativeThreshold), 0f, 1f);
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
    }
}