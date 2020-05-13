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

using UnityEngine;
using System.Collections.Generic;

namespace RPR4U.RPRUnityEditor
{
    internal static class MeshExtension
    {
        private class Vertices
        {
            private List<Vector3> verts = null;
            private List<Vector2> uv1 = null;
            private List<Vector2> uv2 = null;
            private List<Vector2> uv3 = null;
            private List<Vector2> uv4 = null;
            private List<Vector3> normals = null;
            private List<Vector4> tangents = null;
            private List<Color32> colors = null;
            private List<BoneWeight> boneWeights = null;

            public Vertices()
            {
                verts = new List<Vector3>();
            }

            public Vertices(Mesh aMesh)
            {
                verts = CreateList(aMesh.vertices);
                uv1 = CreateList(aMesh.uv);
                uv2 = CreateList(aMesh.uv2);
                uv3 = CreateList(aMesh.uv3);
                uv4 = CreateList(aMesh.uv4);
                normals = CreateList(aMesh.normals);
                tangents = CreateList(aMesh.tangents);
                colors = CreateList(aMesh.colors32);
                boneWeights = CreateList(aMesh.boneWeights);
            }

            private List<T> CreateList<T>(T[] aSource)
            {
                if (aSource == null || aSource.Length == 0)
                    return null;
                return new List<T>(aSource);
            }

            private void Copy<T>(ref List<T> aDest, List<T> aSource, int aIndex)
            {
                if (aSource == null)
                    return;
                if (aDest == null)
                    aDest = new List<T>();
                aDest.Add(aSource[aIndex]);
            }

            public int Add(Vertices aOther, int aIndex)
            {
                int i = verts.Count;
                Copy(ref verts, aOther.verts, aIndex);
                Copy(ref uv1, aOther.uv1, aIndex);
                Copy(ref uv2, aOther.uv2, aIndex);
                Copy(ref uv3, aOther.uv3, aIndex);
                Copy(ref uv4, aOther.uv4, aIndex);
                Copy(ref normals, aOther.normals, aIndex);
                Copy(ref tangents, aOther.tangents, aIndex);
                Copy(ref colors, aOther.colors, aIndex);
                Copy(ref boneWeights, aOther.boneWeights, aIndex);
                return i;
            }

            public void AssignTo(Mesh aTarget)
            {
                if (verts.Count > 65535)
                {
                    aTarget.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                aTarget.SetVertices(verts);

                if (uv1 != null) aTarget.SetUVs(0, uv1);
                if (uv2 != null) aTarget.SetUVs(1, uv2);
                if (uv3 != null) aTarget.SetUVs(2, uv3);
                if (uv4 != null) aTarget.SetUVs(3, uv4);
                if (normals != null) aTarget.SetNormals(normals);
                if (tangents != null) aTarget.SetTangents(tangents);
                if (colors != null) aTarget.SetColors(colors);
                if (boneWeights != null) aTarget.boneWeights = boneWeights.ToArray();
            }
        }

        public static Mesh GetSubmesh(this Mesh aMesh, int aSubMeshIndex)
        {
            if (aSubMeshIndex < 0 || aSubMeshIndex >= aMesh.subMeshCount)
            {
                return null;
            }

            var indices = aMesh.GetTriangles(aSubMeshIndex);

            var source = new Vertices(aMesh);
            var dest = new Vertices();
            var map = new Dictionary<int, int>();

            var newIndices = new int[indices.Length];

            for (int i = 0; i < indices.Length; i++)
            {
                var o = indices[i];

                if (!map.TryGetValue(o, out int n))
                {
                    n = dest.Add(source, o);
                    map.Add(o, n);
                }

                newIndices[i] = n;
            }

            var m = new Mesh();
            dest.AssignTo(m);
            m.triangles = newIndices;

            return m;
        }
    }
}