﻿using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Veldrid;
using Veldrid.Utilities;
using StudioCore.Scene;

namespace StudioCore
{
    public class NewMesh : Scene.IDrawable, IDisposable
    {
        public List<FlverSubmeshRenderer> Submeshes = new List<FlverSubmeshRenderer>();

        private Resource.ResourceHandle<Resource.FlverResource> Resource;
        private bool Created = false;

        //private bool[] DefaultDrawMask = new bool[Model.DRAW_MASK_LENGTH];
        //public bool[] DrawMask = new bool[Model.DRAW_MASK_LENGTH];

        private object _lock_submeshes = new object();

        //public BoundingBox Bounds;

        public bool AutoRegister { get; set; } = true;
        private Scene.RenderScene RenderScene;
        private bool Registered = false;
        private DebugPrimitives.DbgPrimWireBox DebugBoundingBox = null;

        public Scene.ISelectable Selectable { get; set; }

        public bool TextureReloadQueued = false;

        private bool _DebugDrawBounds = false;
        public bool Highlighted
        {
            set
            {
                _DebugDrawBounds = value;
                if (_DebugDrawBounds)
                {
                    if (DebugBoundingBox == null)
                    {
                        DebugBoundingBox = new DebugPrimitives.DbgPrimWireBox(new Transform(_WorldMatrix), Bounds.Min, Bounds.Max, System.Drawing.Color.Red);
                        //RenderScene.AddObject(DebugBoundingBox);
                        Scene.Renderer.AddBackgroundUploadTask((device, cl) =>
                        {
                            DebugBoundingBox.CreateDeviceObjects(device, cl, null);
                        });
                    }
                    else
                    {
                        DebugBoundingBox.EnableDraw = true;
                    }
                }
                else
                {
                    DebugBoundingBox.EnableDraw = false;
                }
            }
            get
            {
                return _DebugDrawBounds;
            }
        }

        private Matrix4x4 _WorldMatrix = Matrix4x4.Identity;
        public Matrix4x4 WorldMatrix
        {
            get
            {
                return _WorldMatrix;
            }
            set
            {
                _WorldMatrix = value;
                OnWorldMatrixChanged();
            }
        }

        public BoundingBox Bounds { get; private set; }
        public RenderFilter DrawFilter { get; set; } = RenderFilter.MapPiece;

        public void DefaultAllMaskValues()
        {
            //for (int i = 0; i < Model.DRAW_MASK_LENGTH; i++)
            //{
            //    DrawMask[i] = DefaultDrawMask[i];
            //}
        }

        public List<string> GetAllTexNamesToLoad()
        {
            List<string> result = new List<string>();

            lock (_lock_submeshes)
            {
                if (Submeshes != null)
                {
                    foreach (var sm in Submeshes)
                    {
                        //result.AddRange(sm.GetAllTexNamesToLoad());
                    }
                }
            }

            
            return result;
        }

        public NewMesh(FLVER2 flver, bool useSecondUV, Dictionary<string, int> boneIndexRemap = null, 
            bool ignoreStaticTransforms = false)
        {
            //LoadFLVER2(flver, useSecondUV, boneIndexRemap, ignoreStaticTransforms);
        }

        public NewMesh(Scene.RenderScene scene, Resource.ResourceHandle<Resource.FlverResource> res, bool useSecondUV, Dictionary<string, int> boneIndexRemap = null,
            bool ignoreStaticTransforms = false)
        {
            RenderScene = scene;
            Resource = res;
            //if (res.IsLoaded)
            //{
            //    CreateSubmeshes();
            //}
            res.AddResourceLoadedHandler((handle) =>
            {
                CreateSubmeshes();
                OnWorldMatrixChanged();
                Renderer.AddBackgroundUploadTask((d, cl) =>
                {
                    foreach (var sm in Submeshes)
                    {
                        sm.CreateDeviceObjects(d, cl, null);
                    }
                    if (AutoRegister)
                    {
                        RegisterWithScene(RenderScene);
                    }
                    Created = true;
                });
            });
        }

        public NewMesh(NewMesh mesh)
        {
            RenderScene = mesh.RenderScene;
            Resource = mesh.Resource;
            Resource.AddResourceLoadedHandler((handle) =>
            {
                CreateSubmeshes();
                OnWorldMatrixChanged();
                Renderer.AddBackgroundUploadTask((d, cl) =>
                {
                    foreach (var sm in Submeshes)
                    {
                        sm.CreateDeviceObjects(d, cl, null);
                    }
                    RegisterWithScene(RenderScene);
                    Created = true;
                });
            });
        }

        private void OnWorldMatrixChanged()
        {
            var res = Resource.Get();
            if (res != null)
            {
                foreach (var sm in Submeshes)
                {
                    sm.WorldTransform = _WorldMatrix;
                }
            }
            if (DebugBoundingBox != null)
            {
                DebugBoundingBox.Transform = new Transform(_WorldMatrix);
            }
            RenderScene.ObjectMoved(this);
        }

        private void CreateSubmeshes()
        {
            lock (_lock_submeshes)
            {
                Submeshes = new List<FlverSubmeshRenderer>();
            }
            var res = Resource.Get();
            Bounds = res.Bounds;
            if (res.GPUMeshes != null)
            {
                for (int i = 0; i < res.GPUMeshes.Length; i++)
                {
                    lock (_lock_submeshes)
                    {
                        var sm = new FlverSubmeshRenderer(this, Resource, i, false);
                        Submeshes.Add(sm);
                    }
                }
            }
            //Bounds = res.Bounds;
        }

        /*private void LoadFLVER2(FLVER2 flver, bool useSecondUV, Dictionary<string, int> boneIndexRemap = null, 
            bool ignoreStaticTransforms = false)
        {
            lock (_lock_submeshes)
            {
                Submeshes = new List<FlverSubmeshRenderer>();
            }
            
            foreach (var submesh in flver.Meshes)
            {
                // Blacklist some materials that don't have good shaders and just make the viewer look like a mess
                MTD mtd = null;// InterrootLoader.GetMTD(Path.GetFileName(flver.Materials[submesh.MaterialIndex].MTD));
                if (mtd != null)
                {
                    if (mtd.ShaderPath.Contains("FRPG_Water_Env"))
                        continue;
                    if (mtd.ShaderPath.Contains("FRPG_Water_Reflect.spx"))
                        continue;
                }
                var smm = new FlverSubmeshRenderer(this, flver, submesh, useSecondUV, boneIndexRemap, ignoreStaticTransforms);

                Bounds = new BoundingBox();

                lock (_lock_submeshes)
                {
                    Submeshes.Add(smm);
                    Bounds = BoundingBox.CreateMerged(Bounds, smm.Bounds);
                }
            }
        }*/

        public List<FlverSubmeshRenderer> GetLoadedSubmeshes()
        {
            if (Submeshes.Count() == 0 && Resource.IsLoaded && Resource.Get().GPUMeshes != null)
            {
                CreateSubmeshes();
                return Submeshes;
            }
            return new List<FlverSubmeshRenderer>();
        }

        public void Draw(int lod = 0, bool motionBlur = false, bool forceNoBackfaceCulling = false, bool isSkyboxLol = false)
        {
            if (Submeshes.Count() == 0 && Resource.IsLoaded)
            {
                CreateSubmeshes();
            }
            if (TextureReloadQueued)
            {
                TryToLoadTextures();
                TextureReloadQueued = false;
            }

            lock (_lock_submeshes)
            {
                if (Submeshes == null)
                    return;

                foreach (var submesh in Submeshes)
                {
                    //submesh.Draw(lod, motionBlur, GFX.FlverShader, DrawMask, forceNoBackfaceCulling, isSkyboxLol);
                }
            }
        }

        public void TryToLoadTextures()
        {
            lock (_lock_submeshes)
            {
                if (Submeshes == null)
                    return;

                //foreach (var sm in Submeshes)
                //    sm.TryToLoadTextures();
            }
        }

        public void Dispose()
        {
            if (Submeshes != null)
            {
                for (int i = 0; i < Submeshes.Count; i++)
                {
                    if (Submeshes[i] != null)
                        Submeshes[i].Dispose();
                }

                Submeshes = null;
            }
        }

        public void SubmitRenderObjects(Renderer.RenderQueue queue)
        {
            if (Submeshes != null && Created)
            {
                foreach (var sm in Submeshes)
                {
                    queue.Add(sm, new RenderKey(0));
                }
                if (DebugBoundingBox != null)
                {
                    queue.Add(DebugBoundingBox, new RenderKey(0));
                }
            }
        }

        public BoundingBox GetBounds()
        {
            return BoundingBox.Transform(Bounds, WorldMatrix);
        }

        public bool RayCast(Ray ray, out float dist)
        {
            var res = Resource.Get();
            if (res != null)
            {
                return res.RayCast(ray, WorldMatrix, out dist);
            }
            dist = float.MaxValue;
            return false;
        }

        public void RegisterWithScene(RenderScene scene)
        {
            if (RenderScene == scene && Registered)
            {
                return;
            }
            else if (RenderScene != scene && Registered)
            {
                UnregisterWithScene();
                RenderScene = scene;
            }
            if (!Resource.IsLoaded)
            {
                return;
            }
            RenderScene.AddObject(this);
            RenderScene.AddOctreeCullable(this);
            Registered = true;
        }

        public void UnregisterWithScene()
        {
            if (Registered)
            {
                RenderScene.RemoveObject(this);
                Registered = false;
            }
        }
    }
}
