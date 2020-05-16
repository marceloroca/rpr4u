// GPL 3 License
//
// Copyright (c) 2020 Marcelo Roca <mru@marceloroca.com>
//
// This file is part of RPR4U (Radeon ProRender for Unity).
//
// RPR4U is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// RPR4U is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with RPR4U. If not, see<https://www.gnu.org/licenses/>.

using RadeonProRender;
using RadeonProRender.OORPR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO.Compression;
using System.IO;

namespace RPR4U.RPRUnityEditor
{
    public class SceneRender : IDisposable
    {
        private RPRContext context;
        private RPRScene scene;
        private RPRFrameBuffer frameBufferRender;
        private RPRFrameBuffer frameBufferTexture;
        private SizeInt imageSize;
        private bool isAccesingBuffer;
        private int numIterations;
        private readonly string pluginFolder;
        private bool stopRender;
        private bool isRenderingFrame;

        public SceneRender(string pluginFolder)
        {
            this.pluginFolder = pluginFolder;
        }

        public bool IsRendering { get; private set; }
        public int IterationCount { get; private set; }
        public bool IsInitialized { get; private set; }

        public TimeSpan RenderingTime { get; private set; }

        public void Dispose()
        {
            this.context?.Dispose();

            UnityEngine.Debug.Log("scenerender Disposed");
        }

        public UnityEngine.Texture2D GetTexture()
        {
            if (this.context == null)
            {
                return null;
            }

            this.isAccesingBuffer = true;

            while (this.isRenderingFrame)
            {
                Thread.Sleep(10);
            }

            this.context.ResolveFrameBuffer(this.frameBufferRender, this.frameBufferTexture, false);

            var tex = new UnityEngine.Texture2D(this.imageSize.Width, this.imageSize.Height, UnityEngine.TextureFormat.RGBAFloat, false);

            this.isAccesingBuffer = false;

            tex.SetPixelData(this.frameBufferTexture.GetImage(), 0, 0);
            tex.filterMode = UnityEngine.FilterMode.Point;
            tex.Apply(updateMipmaps: false);

            return tex;
        }

        public void Initialize(CreationFlags creationFlags, RenderMode renderMode, CameraMode cameraMode, int cameraId, float ipd, int width, int height, int numIterations, int adaptativeMinSamples, int adaptativeTileSize, float adaptativeThreshold, float direcionalLigthMultiplier)
        {
            if (this.context != null)
            {
                this.context.Dispose();
                this.frameBufferRender = null;
                this.frameBufferTexture = null;
                this.scene = null;
            }

            this.numIterations = numIterations;

            this.imageSize = new SizeInt(width, height);

            var in_props = new IntPtr[16];
            in_props[0] = (IntPtr)ContextInfo.SamplerType;
            in_props[1] = (IntPtr)ContextSamplerType.Cmj;
            in_props[2] = (IntPtr)ContextInfo.XFlip;
            in_props[3] = (IntPtr)1u;
            in_props[4] = (IntPtr)0;

            this.context = new RPRContext(creationFlags, this.pluginFolder, in_props);

            this.context.SetRenderMode(renderMode);
            this.context.SetParameter("preview", 1u);

            ///////// Adaptative Sampling //////////////
            //Size of the sampling area in pixels – recommended: [4, 16]
            this.context.SetParameter("as.tilesize", adaptativeTileSize);

            //Minimum number of samples per pixel before activating Adaptive sampling
            this.context.SetParameter("as.minspp", adaptativeMinSamples);

            //Tolerance of the Adaptive Sampler
            this.context.SetParameter("as.threshold", adaptativeThreshold);

            this.scene = new RPRScene(this.context);
            this.scene.SetCurrent();

            var sceneData = SceneData.GenerateSceneData();
            sceneData.Update();

            this.frameBufferRender = new RPRFrameBuffer(context, new FramebufferFormat(4, ComponentType.Float32), new FramebufferDesc(this.imageSize));
            context.SetAOV(this.frameBufferRender, AOV.Color);

            this.frameBufferTexture = new RPRFrameBuffer(context, new FramebufferFormat(4, ComponentType.Float32), new FramebufferDesc(this.imageSize));

            var postEffect = new RPRPostEffect(context, PostEffectType.Normalization);
            context.Attach(postEffect);

            var materialTable = new Dictionary<int, RPRMaterial>();

            foreach (var itemMaterial in sceneData.Materials)
            {
                if (!materialTable.ContainsKey(itemMaterial.Key))
                {
                    RPRMaterial rprMaterial;

                    if (string.IsNullOrEmpty(itemMaterial.Value.MainTexture))
                    {
                        rprMaterial = new RPRUberMaterial(this.context, UberMaterial.DiffuseSimple(itemMaterial.Value.MainColor));
                    }
                    else
                    {
                        rprMaterial = new RPRUberMaterial(this.context, UberMaterial.DiffuseSimple(itemMaterial.Value.MainTexture));
                    }

                    materialTable.Add(itemMaterial.Key, rprMaterial);
                }
            }

            foreach (var itemMesh in sceneData.Meshes)
            {
                var csm = 0;

                foreach (var itemSubMesh in itemMesh.Value.SubMeshes)
                {
                    // creamos el mesh
                    var rprMesh = new RPRMesh(this.context, itemSubMesh);

                    // instanciamos
                    var cin = 0; ;
                    foreach (var itemInstance in itemMesh.Value.Instances)
                    {
                        if (cin < 1)
                        {
                            rprMesh.SetTRS(itemInstance.Transform);
                            scene.Attach(rprMesh);
                            // le metemos el material
                            rprMesh.SetMaterial(materialTable[itemInstance.MaterialsId[csm]]);
                        }
                        else
                        {
                            var rprInstance = new RPRMeshInstance(this.context, rprMesh);
                            rprInstance.SetTRS(itemInstance.Transform);
                            scene.Attach(rprInstance);
                            // le metemos el material
                            rprInstance.SetMaterial(materialTable[itemInstance.MaterialsId[csm]]);
                        }

                        ++cin;
                    }

                    ++csm;
                }
            }

            // creamos lights
            foreach (var itemLigh in sceneData.Lights)
            {
                RPRLight rprLight = null;

                switch (itemLigh.Type)
                {
                    case LightType.Spot:
                        var rprSpotLight = new RPRSpotLight(this.context);
                        rprSpotLight.SetRadiantPower(itemLigh.Color * itemLigh.Intensity * 1);
                        rprSpotLight.SetConeShape(itemLigh.InnerSpotAngle, itemLigh.OuterSpotAngle);

                        rprLight = rprSpotLight;
                        break;

                    case LightType.Point:
                        var rprPointLight = new RPRPointLight(this.context);
                        rprPointLight.SetRadiantPower(itemLigh.Color * itemLigh.Intensity * 4);

                        rprLight = rprPointLight;
                        break;

                    case LightType.Directional:
                        var rprDirectionalLight = new RPRDirectionalLight(this.context);
                        rprDirectionalLight.SetRadiantPower(itemLigh.Color * itemLigh.Intensity * direcionalLigthMultiplier);

                        switch (itemLigh.ShadowType)
                        {
                            case ShadowType.None:
                                rprDirectionalLight.SetShadowSoftness(0);
                                break;

                            case ShadowType.Soft:
                                rprDirectionalLight.SetShadowSoftness(.5f);
                                break;

                            case ShadowType.Hard:
                                rprDirectionalLight.SetShadowSoftness(1f);
                                break;
                        }

                        rprLight = rprDirectionalLight;
                        break;

                    default:
                        UnityEngine.Debug.LogError($"Light {itemLigh.Type} not implemented");
                        break;
                }

                if (rprLight != null)
                {
                    rprLight.SetTRS(itemLigh.Transform);
                    scene.Attach(rprLight);
                }
            }

            var cameras = new Dictionary<int, RPRCamera>();

            foreach (var itemCamera in sceneData.Cameras)
            {
                var camera = new RPRCamera(this.context);

                camera.SetTRS(itemCamera.Value.Transform);

                camera.SetFocalLength(itemCamera.Value.FocalLenght);

                var sensorSize = itemCamera.Value.SensorSize;

                camera.SetSensorSize(new SizeFloat(sensorSize.Width, sensorSize.Width * imageSize.AspectRatio()));

                camera.SetLensShift(itemCamera.Value.LensShift);
                camera.SetClippingPlanes(itemCamera.Value.ClippingPlanes.X, itemCamera.Value.ClippingPlanes.Y);

                cameras.Add(itemCamera.Key, camera);
            }

            if (cameras.TryGetValue(cameraId, out RPRCamera selectedCamera))
            {
                this.scene.SetCurrentCamera(selectedCamera);
                selectedCamera.SetMode(cameraMode);

                if (cameraMode == CameraMode.LatitudLongitudeStereo || cameraMode == CameraMode.CubemapStereo)
                {
                    selectedCamera.SetIPD(ipd);
                }
            }

            this.IsInitialized = true;
        }

        public void SaveRender(string path)
        {
            this.frameBufferRender?.SaveToFile(path);
        }

        public void ExportScene(string path, bool compress)
        {
            this.scene?.Export(path);

            if (compress)
            {
                File.Delete(path + ".z");

                using var zip = ZipFile.Open(path + ".z", ZipArchiveMode.Create);
                var fileInfo = new FileInfo(path);
                zip.CreateEntryFromFile(path, fileInfo.Name, CompressionLevel.Optimal);
            }
        }

        public void StartRender()
        {
            this.IsRendering = true;

            this.stopRender = false;
            this.IterationCount = 0;
            var startTime = DateTime.Now;

            var renderThread = new Thread(() =>
            {
                while (!this.stopRender)
                {
                    while (this.isAccesingBuffer)
                    {
                        Thread.Sleep(10);
                    }

                    this.isRenderingFrame = true;
                    this.context.SetParameter("framecount", this.IterationCount);
                    this.context.Render();
                    ++this.IterationCount;
                    this.isRenderingFrame = false;

                    this.RenderingTime = DateTime.Now - startTime;

                    if (this.IterationCount > this.numIterations)
                    {
                        this.stopRender = true;
                    }

                    Thread.Sleep(10);
                }

                this.IsRendering = false;
            });

            renderThread.Start();
        }

        public void StopRender()
        {
            this.stopRender = true;

            while (this.isRenderingFrame)
            {
                Thread.Sleep(10);
            }
        }
    }
}