﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TSOClient.VM;
using TSOClient.Code.Data.Model;
using SimsLib.ThreeD;
using Microsoft.Xna.Framework.Graphics;

namespace TSOClient.Code.Data
{
    /// <summary>
    /// Place to get information and assets related to sims, e.g. skins, thumbnails etc
    /// </summary>
    public class SimCatalog
    {
        public static void GetCollection(ulong fileID)
        {
            var collectionData = ContentManager.GetResourceFromLongID(fileID);
            var reader = new BinaryReader(new MemoryStream(collectionData));
        }

        public SimCatalog()
        {
        }

        private static Dictionary<ulong, TSOClient.Code.Data.Model.Outfit> Outfits = new Dictionary<ulong, TSOClient.Code.Data.Model.Outfit>();
        public static TSOClient.Code.Data.Model.Outfit GetOutfit(ulong id)
        {
            if (Outfits.ContainsKey(id))
            {
                return Outfits[id];
            }

            var bytes = ContentManager.GetResourceFromLongID(id);
            var outfit = new TSOClient.Code.Data.Model.Outfit(bytes);
            Outfits.Add(id, outfit);
            return outfit;
        }

        private static Dictionary<ulong, Texture2D> OutfitTextures = new Dictionary<ulong, Texture2D>();
        public static Texture2D GetOutfitTexture(ulong id)
        {
            if (OutfitTextures.ContainsKey(id))
            {
                return OutfitTextures[id];
            }

            var bytes = ContentManager.GetResourceFromLongID(id);
            using (var stream = new MemoryStream(bytes))
            {
                var texture = Texture2D.FromFile(GameFacade.GraphicsDevice, stream);
                OutfitTextures.Add(id, texture);
                return texture;
            }
        }

        private static Dictionary<ulong, Mesh> OutfitMeshes = new Dictionary<ulong, Mesh>();
        public static Mesh GetOutfitMesh(ulong id)
        {
            if (OutfitMeshes.ContainsKey(id))
            {
                return OutfitMeshes[id];
            }
            
            var mesh = new Mesh();
            mesh.Read(ContentManager.GetResourceFromLongID(id));
            mesh.ProcessMesh();
            OutfitMeshes.Add(id, mesh);
            return mesh;
        }

        public static void LoadSim3D(Sim sim, TSOClient.Code.Data.Model.Outfit OutfHead, AppearanceType skin)
        {
            var Apr = OutfHead.GetAppearance(skin);
            var Bnd = new Binding(ContentManager.GetResourceFromLongID(Apr.BindingIDs[0]));

            sim.HeadTexture = GetOutfitTexture(Bnd.TextureAssetID);
            sim.HeadMesh = GetOutfitMesh(Bnd.MeshAssetID);
        }
    }
}