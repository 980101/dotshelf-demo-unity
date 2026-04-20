using UnityEngine;
using UnityEngine.Rendering;

namespace BookshelfPorting.Runtime
{
    [DisallowMultipleComponent]
    public class RoomEditableObject : MonoBehaviour
    {
        private const float ColliderMinHeight = 0.08f;
        private const float ColliderMinWidth = 0.12f;
        private const float FramePadding = 0.035f;
        private const float FrameMinHeight = 0.08f;
        private const float FrameMinThickness = 0.01f;
        private const float FrameMaxThickness = 0.026f;

        [SerializeField] private RoomPlaceableType placeableType;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Vector3 localBoundsCenter = Vector3.zero;
        [SerializeField] private Vector3 localBoundsSize = Vector3.one;
        [SerializeField] private float floorHeight;
        [SerializeField] private BoxCollider interactionCollider = null;
        [SerializeField] private GameObject selectionFrame = null;

        public RoomPlaceableType PlaceableType => placeableType;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? GetFallbackName(placeableType) : displayName;
        public BoxCollider InteractionCollider => interactionCollider;
        public float FloorHeight => floorHeight;

        public void Initialize(RoomPlaceableType type, string label, Bounds localBounds, Material highlightMaterial)
        {
            placeableType = type;
            displayName = label;
            localBoundsCenter = localBounds.center;
            localBoundsSize = localBounds.size;
            floorHeight = transform.position.y;

            EnsureInteractionCollider();
            RebuildSelectionFrame(highlightMaterial);
            SetHighlighted(false);
        }

        public void SetHighlighted(bool isHighlighted)
        {
            if (selectionFrame != null)
            {
                selectionFrame.SetActive(isHighlighted);
            }
        }

        private void EnsureInteractionCollider()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<BoxCollider>();
            }

            if (interactionCollider == null)
            {
                interactionCollider = gameObject.AddComponent<BoxCollider>();
            }

            interactionCollider.center = localBoundsCenter;

            var colliderSize = localBoundsSize;
            colliderSize.x = Mathf.Max(colliderSize.x, ColliderMinWidth);
            colliderSize.y = Mathf.Max(colliderSize.y, ColliderMinHeight);
            colliderSize.z = Mathf.Max(colliderSize.z, ColliderMinWidth);
            interactionCollider.size = colliderSize;
        }

        private void RebuildSelectionFrame(Material highlightMaterial)
        {
            if (selectionFrame != null)
            {
                DestroyUnityObject(selectionFrame);
            }

            selectionFrame = new GameObject("SelectionFrame");
            selectionFrame.transform.SetParent(transform, false);
            selectionFrame.transform.localPosition = localBoundsCenter;
            selectionFrame.transform.localRotation = Quaternion.identity;
            selectionFrame.transform.localScale = Vector3.one;

            var frameSize = localBoundsSize + Vector3.one * FramePadding;
            frameSize.x = Mathf.Max(frameSize.x, ColliderMinWidth);
            frameSize.y = Mathf.Max(frameSize.y, FrameMinHeight);
            frameSize.z = Mathf.Max(frameSize.z, ColliderMinWidth);

            var thickness = Mathf.Clamp(Mathf.Min(frameSize.x, frameSize.y, frameSize.z) * 0.18f, FrameMinThickness, FrameMaxThickness);
            var half = frameSize * 0.5f;

            CreateEdge(selectionFrame.transform, new Vector3(0f, half.y, half.z), new Vector3(frameSize.x, thickness, thickness), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(0f, half.y, -half.z), new Vector3(frameSize.x, thickness, thickness), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(0f, -half.y, half.z), new Vector3(frameSize.x, thickness, thickness), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(0f, -half.y, -half.z), new Vector3(frameSize.x, thickness, thickness), highlightMaterial);

            CreateEdge(selectionFrame.transform, new Vector3(half.x, 0f, half.z), new Vector3(thickness, frameSize.y, thickness), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(half.x, 0f, -half.z), new Vector3(thickness, frameSize.y, thickness), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(-half.x, 0f, half.z), new Vector3(thickness, frameSize.y, thickness), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(-half.x, 0f, -half.z), new Vector3(thickness, frameSize.y, thickness), highlightMaterial);

            CreateEdge(selectionFrame.transform, new Vector3(half.x, half.y, 0f), new Vector3(thickness, thickness, frameSize.z), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(-half.x, half.y, 0f), new Vector3(thickness, thickness, frameSize.z), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(half.x, -half.y, 0f), new Vector3(thickness, thickness, frameSize.z), highlightMaterial);
            CreateEdge(selectionFrame.transform, new Vector3(-half.x, -half.y, 0f), new Vector3(thickness, thickness, frameSize.z), highlightMaterial);
        }

        private static void CreateEdge(Transform parent, Vector3 localPosition, Vector3 localScale, Material highlightMaterial)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "FrameEdge";
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = localPosition;
            edge.transform.localRotation = Quaternion.identity;
            edge.transform.localScale = localScale;

            var collider = edge.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyUnityObject(collider);
            }

            var renderer = edge.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = highlightMaterial != null ? highlightMaterial : CreateFallbackHighlightMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material CreateFallbackHighlightMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader);
            var color = new Color(0.96f, 0.64f, 0.18f, 1f);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
            return material;
        }

        private static string GetFallbackName(RoomPlaceableType type)
        {
            switch (type)
            {
                case RoomPlaceableType.BookStack:
                    return "Book Stack";
                case RoomPlaceableType.FloorLamp:
                    return "Floor Lamp";
                case RoomPlaceableType.NaturalRug:
                    return "Natural Rug";
                default:
                    return "Hamham";
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
