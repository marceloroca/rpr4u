using RadeonProRender;
using RadeonProRender.OORPR;
using System;

namespace RPR4U.RPRUnityEditor
{
    public static class ConvertExtensions
    {
        public static CameraData Convert(this UnityEngine.Camera camera)
        {
            //var rot = camera.transform.rotation * UnityEngine.Quaternion.Euler(0, 180, 180);
            var rot = camera.transform.rotation * UnityEngine.Quaternion.Euler(0, 180, 0);

            return new CameraData
            {
                CameraMode = CameraMode.Perspective,
                FocalLenght = camera.focalLength,
                SensorSize = camera.sensorSize.ToSizeFloat(),
                LensShift = camera.lensShift.Convert(),
                ClippingPlanes = new Vector2(camera.nearClipPlane, camera.farClipPlane),
                Transform = new TransformData(camera.transform.position.Convert(), rot.Convert(), camera.transform.lossyScale.Convert())
                //Transform = camera.transform.Convert()
            };
        }

        public static Mesh Convert(this UnityEngine.Mesh source)
        {
            var dest = new Mesh
            {
                Vertices = Array.ConvertAll(source.vertices, v => v.Convert()),
                Normals = Array.ConvertAll(source.normals, n => n.Convert()),
                TexCoords = Array.ConvertAll(source.uv, t => t.Convert()),
                Indices = source.triangles,
                NumFaceVertices = new int[source.triangles.Length / 3]
            };

            dest.NumFaceVertices.Populate<int>(3);

            return dest;
        }

        public static T[] Populate<T>(this T[] arr, T value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }

            return arr;
        }

        public static SizeFloat ToSizeFloat(this UnityEngine.Vector2 source)
        {
            return new SizeFloat(source.x, source.y);
        }

        public static Vector2 Convert(this UnityEngine.Vector2 source)
        {
            return new Vector2(source.x, source.y);
        }

        public static Vector3 Convert(this UnityEngine.Vector3 source)
        {
            return new Vector3(source.x, source.y, source.z);
        }

        public static Quaternion Convert(this UnityEngine.Quaternion source)
        {
            return new Quaternion(source.x, source.y, source.z, source.w);
        }

        public static Color Convert(this UnityEngine.Color source)
        {
            return new Color(source.r, source.g, source.b, source.a);
        }

        public static TransformData Convert(this UnityEngine.Transform source)
        {
            return new TransformData(source.position.Convert(), source.rotation.Convert(), source.lossyScale.Convert());
        }

        public static LightType Convert(this UnityEngine.LightType type)
        {
            return type switch
            {
                UnityEngine.LightType.Directional => LightType.Directional,
                UnityEngine.LightType.Point => LightType.Point,
                UnityEngine.LightType.Spot => LightType.Spot,
                _ => throw new NotImplementedException($"{type} not implemented"),
            };
        }
    }
}