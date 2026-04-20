using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public enum BookshelfThemeStyle
    {
        Default,
        Warm,
        Dark
    }

    public class MaterialFactory : MonoBehaviour
    {
        private const string FloorTexturePath = "Assets/Arts/Textures/Floor/floor.png";
        private const string WallTopTexturePath = "Assets/Arts/Textures/Wall/wall_top_check_beige.png";
        private const string WallBottomTexturePath = "Assets/Arts/Textures/Wall/wall_bottom_panel_ivory.png";
        private const string WallTrimTexturePath = "Assets/Arts/Textures/Wall/wall_trim_molding_ivory.png";

        [Header("Wood")]
        [SerializeField] private Texture2D woodBaseMap = null;
        [SerializeField] private Texture2D woodNormalMap = null;
        [SerializeField] private float bookshelfSmoothness = 0.42f;
        [SerializeField] private float floorSmoothness = 0.18f;

        [Header("Flat Colors")]
        [SerializeField] private Color wallColor = new Color(0.93f, 0.90f, 0.84f);
        [SerializeField] private Color wallTopTint = new Color(1f, 0.995f, 0.985f);
        [SerializeField] private Color wallBottomTint = new Color(1f, 0.99f, 0.975f);
        [SerializeField] private Color wallTrimTint = new Color(0.995f, 0.985f, 0.965f);
        [SerializeField] private Color whiteboardFrameColor = new Color(0.34f, 0.26f, 0.18f);
        [SerializeField] private Color whiteboardSurfaceColor = new Color(0.96f, 0.97f, 0.95f);
        [SerializeField] private Color ghostColor = new Color(0.25f, 0.85f, 1f, 0.25f);
        [SerializeField] private Color bookshelfTint = Color.white;

        [Header("Wall Textures")]
        [SerializeField] private Texture2D floorBaseMap = null;
        [SerializeField] private Texture2D wallTopBaseMap = null;
        [SerializeField] private Texture2D wallBottomBaseMap = null;
        [SerializeField] private Texture2D wallTrimBaseMap = null;

        [Header("Floor Tiling")]
        [SerializeField] private Vector2 floorTiling = new Vector2(3.0f, 3.0f);

        [Header("Wall Tiling")]
        [SerializeField] private Vector2 wallTopTiling = new Vector2(4.5f, 3f);
        [SerializeField] private Vector2 wallBottomTiling = new Vector2(2.6f, 1.1f);
        [SerializeField] private Vector2 wallTrimTiling = new Vector2(6.5f, 1f);
        [SerializeField] private float wallSectionSmoothness = 0.03f;

        private Material bookshelfWoodMaterial;
        private Material furnitureWoodMaterial;
        private Material floorMaterial;
        private Material wallMaterial;
        private Material wallTopMaterial;
        private Material wallBottomMaterial;
        private Material wallTrimMaterial;
        private Material whiteboardFrameMaterial;
        private Material whiteboardSurfaceMaterial;
        private Material ghostMaterial;
        public BookshelfThemeStyle CurrentTheme { get; private set; } = BookshelfThemeStyle.Default;

        public void Configure(Texture2D woodBase, Texture2D woodNormal)
        {
            woodBaseMap = woodBase;
            woodNormalMap = woodNormal;
        }

        public Material GetBookshelfWoodMaterial()
        {
            if (bookshelfWoodMaterial == null)
            {
                bookshelfWoodMaterial = CreateLitMaterial("BookshelfWood", woodBaseMap, woodNormalMap, bookshelfTint, bookshelfSmoothness, false);
            }

            return bookshelfWoodMaterial;
        }

        public Material GetFloorMaterial()
        {
            if (floorMaterial == null)
            {
                EnsureFloorTextureLoaded();
                floorMaterial = floorBaseMap != null
                    ? CreateTiledLitMaterial("FloorWood", floorBaseMap, Color.white, floorSmoothness, floorTiling)
                    : CreateLitMaterial("FloorWood", woodBaseMap, woodNormalMap, Color.white, floorSmoothness, false);
            }

            return floorMaterial;
        }

        public Material GetFurnitureWoodMaterial()
        {
            if (furnitureWoodMaterial == null)
            {
                furnitureWoodMaterial = CreateLitMaterial("FurnitureWood", woodBaseMap, woodNormalMap, Color.white, bookshelfSmoothness, false);
            }

            return furnitureWoodMaterial;
        }

        public Material GetWallMaterial()
        {
            if (wallMaterial == null)
            {
                wallMaterial = CreateLitMaterial("Wall", null, null, wallColor, 0.08f, false);
            }

            return wallMaterial;
        }

        public Material GetWallTopMaterial()
        {
            if (wallTopMaterial == null)
            {
                EnsureWallTexturesLoaded();
                wallTopMaterial = CreateTiledLitMaterial("WallTopCheck", wallTopBaseMap, wallTopTint, wallSectionSmoothness, wallTopTiling);
            }

            return wallTopMaterial;
        }

        public Material GetWallBottomMaterial()
        {
            if (wallBottomMaterial == null)
            {
                EnsureWallTexturesLoaded();
                wallBottomMaterial = CreateTiledLitMaterial("WallBottomPanel", wallBottomBaseMap, wallBottomTint, wallSectionSmoothness, wallBottomTiling);
            }

            return wallBottomMaterial;
        }

        public Material GetWallTrimMaterial()
        {
            if (wallTrimMaterial == null)
            {
                EnsureWallTexturesLoaded();
                wallTrimMaterial = CreateTiledLitMaterial("WallTrimMolding", wallTrimBaseMap, wallTrimTint, wallSectionSmoothness, wallTrimTiling);
            }

            return wallTrimMaterial;
        }

        public Material GetWhiteboardFrameMaterial()
        {
            if (whiteboardFrameMaterial == null)
            {
                whiteboardFrameMaterial = CreateLitMaterial("WhiteboardFrame", null, null, whiteboardFrameColor, 0.32f, false);
            }

            return whiteboardFrameMaterial;
        }

        public Material GetWhiteboardSurfaceMaterial()
        {
            if (whiteboardSurfaceMaterial == null)
            {
                whiteboardSurfaceMaterial = CreateLitMaterial("WhiteboardSurface", null, null, whiteboardSurfaceColor, 0.62f, false);
            }

            return whiteboardSurfaceMaterial;
        }

        public Material GetGhostPreviewMaterial()
        {
            if (ghostMaterial == null)
            {
                ghostMaterial = CreateLitMaterial("GhostPreview", null, null, ghostColor, 0.1f, true);
            }

            return ghostMaterial;
        }

        public Material CreateBookMaterial(Color color)
        {
            return CreateLitMaterial("BookDynamic", null, null, color, 0.3f, false);
        }

        public Material CreateBookMaterial(Texture2D baseMap, Texture2D normalMap, Color color, float smoothness = 0.3f)
        {
            return CreateLitMaterial("BookTextured", baseMap, normalMap, color, smoothness, false);
        }

        public void ApplyTheme(BookshelfThemeStyle theme)
        {
            CurrentTheme = theme;

            switch (theme)
            {
                case BookshelfThemeStyle.Warm:
                    bookshelfTint = new Color(1f, 0.88f, 0.74f);
                    break;
                case BookshelfThemeStyle.Dark:
                    bookshelfTint = new Color(0.36f, 0.30f, 0.26f);
                    break;
                default:
                    bookshelfTint = Color.white;
                    break;
            }

            ApplyThemeToMaterial(bookshelfWoodMaterial, bookshelfTint, bookshelfSmoothness);
        }

        private static void ApplyThemeToMaterial(Material material, Color color, float smoothness)
        {
            if (material == null)
            {
                return;
            }

            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
        }

        private void EnsureWallTexturesLoaded()
        {
#if UNITY_EDITOR
            if (wallTopBaseMap == null)
            {
                wallTopBaseMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(WallTopTexturePath);
            }

            if (wallBottomBaseMap == null)
            {
                wallBottomBaseMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(WallBottomTexturePath);
            }

            if (wallTrimBaseMap == null)
            {
                wallTrimBaseMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(WallTrimTexturePath);
            }
#endif

            ConfigureTextureForTiling(wallTopBaseMap);
            ConfigureTextureForTiling(wallBottomBaseMap);
            ConfigureTextureForTiling(wallTrimBaseMap);
        }

        private void EnsureFloorTextureLoaded()
        {
#if UNITY_EDITOR
            if (floorBaseMap == null)
            {
                floorBaseMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexturePath);
            }
#endif

            ConfigureTextureForTiling(floorBaseMap);
        }

        private static Material CreateTiledLitMaterial(string name, Texture2D baseMap, Color color, float smoothness, Vector2 tiling)
        {
            var material = CreateLitMaterial(name, baseMap, null, color, smoothness, false);
            material.SetTextureScale("_BaseMap", tiling);
            material.mainTextureScale = tiling;
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            return material;
        }

        private static void ConfigureTextureForTiling(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = Mathf.Max(texture.anisoLevel, 2);
        }

        private static Material CreateLitMaterial(string name, Texture2D baseMap, Texture2D normalMap, Color color, float smoothness, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);

            if (baseMap != null)
            {
                material.SetTexture("_BaseMap", baseMap);
            }

            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normalMap);
                material.SetFloat("_BumpScale", 1f);
            }

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return material;
        }
    }
}
