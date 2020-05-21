using Newtonsoft.Json;
using RPR4U.RPRUnityEditor.Data;
using System;
using System.Collections;
using System.IO;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace RPR4U.RPRUnityEditor
{
    public partial class ViewportWindow : EditorWindow
    {
        private const int ZOOM_MAX = 400;
        private const int ZOOM_MIN = 50;
        private Rect lightMultipliergButtonRect;
        private Vector2? mousePosition;
        private Vector2 previewZoomPosition = new Vector2(0, 0);
        private Texture2D renderTexture;
        private SceneRender sceneRender;
        private SceneSettings settings;
        private EditorCoroutine updateCoroutine;

        private bool previewImageFit
        {
            get { return EditorPrefs.GetBool("rpr4u.viewport.previewImageFit", true); }
            set { EditorPrefs.SetBool("rpr4u.viewport.previewImageFit", value); }
        }

        private int previewZoomLevel
        {
            get { return EditorPrefs.GetInt("rpr4u.viewport.previewZoomLevel", 100); }
            set { EditorPrefs.SetInt("rpr4u.viewport.previewZoomLevel", value); }
        }

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
                        this.settings = SceneSettings.Default;

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
            GetWindow(typeof(ViewportWindow), false, "RPR Viewport");
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
            if (this.renderTexture == null)
            {
                this.previewZoomLevel = 100;
                this.mousePosition = null;
            }
            else
            {
                var windowRect = new Rect(0, 21, this.position.width, this.position.height - 42);
                var mouseInRect = windowRect.Contains(Event.current.mousePosition);

                if (mouseInRect)
                {
                    // zoom
                    if (Event.current.type == EventType.ScrollWheel)
                    {
                        var newZoomLevel = Mathf.Clamp(this.previewZoomLevel - (int)Event.current.delta.y * 3, ZOOM_MIN, ZOOM_MAX);

                        var delta = (newZoomLevel - this.previewZoomLevel) * .01f;

                        var dw = this.renderTexture.width * delta;
                        var dh = this.renderTexture.height * delta;

                        var w = this.renderTexture.width * this.previewZoomLevel * .01f;
                        var h = this.renderTexture.height * this.previewZoomLevel * .01f;

                        var x = .5f;
                        var y = .5f;

                        //x = (w - this.previewZoomPosition.x) / w;
                        //y = (h - this.previewZoomPosition.y) / h;

                        this.previewZoomPosition.x -= dw * x;
                        this.previewZoomPosition.y -= dh * y;

                        this.previewZoomLevel = newZoomLevel;

                        Event.current.Use();
                    }

                    // pan
                    if (Event.current.button == 0)
                    {
                        if (Event.current.type == EventType.MouseDrag)
                        {
                            if (!this.mousePosition.HasValue)
                            {
                                this.mousePosition = Event.current.mousePosition;
                            }
                            else
                            {
                                this.previewZoomPosition = this.previewZoomPosition + Event.current.mousePosition - this.mousePosition.Value;
                                this.mousePosition = Event.current.mousePosition;
                            }

                            Event.current.Use();
                        }

                        if (Event.current.type == EventType.MouseUp)
                        {
                            this.mousePosition = null;
                            Event.current.Use();
                        }
                    }
                }
                else
                {
                    this.mousePosition = null;
                }

                if (this.previewImageFit)
                {
                    this.DrawTexturePreviewFit();
                }
                else
                {
                    this.DrawTexturePreviewZoom();
                }
            }

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
                                File.WriteAllBytes(path, this.renderTexture.EncodeToPNG());
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

            EditorGUIUtility.labelWidth = 60;
            EditorGUIUtility.fieldWidth = 10;
            this.previewImageFit = EditorGUILayout.Toggle("Image Fit: ", this.previewImageFit);

            if (!this.previewImageFit)
            {
                EditorGUIUtility.labelWidth = 70;
                EditorGUIUtility.fieldWidth = 30;
                this.previewZoomLevel = EditorGUILayout.IntSlider("Zoom level: ", this.previewZoomLevel, ZOOM_MIN, ZOOM_MAX);
            }

            //////
            if (GUILayout.Button("Reset"))
            {
                this.previewZoomPosition = Vector2.zero;
                this.previewZoomLevel = 100;
            }

            if (this.renderTexture != null)
            {
                EditorGUIUtility.labelWidth = 300;
                EditorGUILayout.LabelField($"xx:{this.previewZoomPosition} size: {new Vector2(this.renderTexture.width, this.renderTexture.height) * (this.previewZoomLevel * .01f)} w: {this.position.width}, {this.position.height}");
            }

            ///////

            GUILayout.FlexibleSpace();

            if (this.SceneRender.IterationCount > 0)
            {
                if (this.SceneRender.IsRendering)
                {
                    var completion = this.SceneRender.IterationCompletion * 100;
                    var iterationsPerSecond = this.SceneRender.IterationCount / this.SceneRender.RenderingTime.TotalSeconds;
                    var timeLeft = TimeSpan.FromSeconds((this.SceneRender.IterationLenght - this.SceneRender.IterationCount) / iterationsPerSecond);

                    EditorGUIUtility.labelWidth = 45;
                    EditorGUILayout.LabelField($"Render stats:");
                    EditorGUIUtility.labelWidth = 20;
                    EditorGUILayout.LabelField($"{completion:#0.00}%");
                    EditorGUIUtility.labelWidth = 70;
                    EditorGUILayout.LabelField($"Time = {this.SceneRender.RenderingTime:hh\\:mm\\.ss}");
                    EditorGUIUtility.labelWidth = 70;
                    EditorGUILayout.LabelField($"Left = {timeLeft:hh\\:mm\\.ss}");
                    EditorGUIUtility.labelWidth = 55;
                    EditorGUILayout.LabelField($"Speed = {(this.SceneRender.IterationCount / this.SceneRender.RenderingTime.TotalSeconds):#0.00}");
                }
                else
                {
                    EditorGUIUtility.labelWidth = 70;
                    EditorGUILayout.LabelField($"Last render stats:");
                    EditorGUIUtility.labelWidth = 70;
                    EditorGUILayout.LabelField($"Time = {this.SceneRender.RenderingTime:hh\\:mm\\.ss}");
                    EditorGUIUtility.labelWidth = 55;
                    EditorGUILayout.LabelField($"Speed = {(this.SceneRender.IterationCount / this.SceneRender.RenderingTime.TotalSeconds):#0.00}");
                }
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

        private void DrawTexturePreviewFit()
        {
            if (this.renderTexture == null)
            {
                return;
            }

            var w = (this.position.width) / this.renderTexture.width;
            var h = (this.position.height - 42) / this.renderTexture.height;

            var m = Mathf.Min(w, h);

            var w2 = this.renderTexture.width * m;
            var h2 = this.renderTexture.height * m;

            var x = 0f;
            var y = 21f;

            if (w > h)
            {
                x += (this.position.width - w2) * .5f;
            }
            else if (h > w)
            {
                y += (this.position.height - h2 - 42) * .5f;
            }

            EditorGUI.DrawPreviewTexture(new Rect(x, y, w2, h2), this.renderTexture);
        }

        private void DrawTexturePreviewZoom()
        {
            if (this.renderTexture == null)
            {
                return;
            }

            // zoom
            var w = this.renderTexture.width * this.previewZoomLevel * .01f;
            var h = this.renderTexture.height * this.previewZoomLevel * .01f;

            // pan
            if (w <= this.position.width)
            {
                // center the image horizontally
                this.previewZoomPosition.x = (this.position.width - w) * .5f;
            }
            else
            {
                // clamp the image horizontally
                this.previewZoomPosition.x = Mathf.Clamp(this.previewZoomPosition.x, -(w - this.position.width), 0);
            }

            if (h <= this.position.height)
            {
                // center de image vertically
                this.previewZoomPosition.y = (this.position.height - h - 42) * .5f;
            }
            else
            {
                // clamp the image vertically
                this.previewZoomPosition.y = Mathf.Clamp(this.previewZoomPosition.y, -(h - this.position.height), 0);
            }

            var x = previewZoomPosition.x;
            var y = this.previewZoomPosition.y + 21f;

            EditorGUI.DrawPreviewTexture(new Rect(x, y, w, h), this.renderTexture);
        }

        private void InitializeScene()
        {
            this.SceneRender.Initialize(RadeonProRender.CreationFlags.Gpu00 | RadeonProRender.CreationFlags.Gpu01, this.Settings);
        }

        private Texture2D RotateTexture(Texture2D source, TextureFormat textureFormat = TextureFormat.RGBA32)
        {
            var pix = source.GetPixels();
            Array.Reverse(pix, 0, pix.Length);

            var rotatedTexture = new Texture2D(source.width, source.height, textureFormat, false, true);
            rotatedTexture.SetPixels(pix);
            rotatedTexture.Apply();

            return rotatedTexture;
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
                    this.renderTexture = this.RotateTexture(this.sceneRender.GetTexture());
                    this.Repaint();
                }

                yield return new EditorWaitForSeconds(.1f);
            }
        }
    }
}