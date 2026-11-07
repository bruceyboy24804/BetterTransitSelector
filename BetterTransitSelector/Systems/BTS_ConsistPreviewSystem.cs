namespace BetterTransitSelector.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using Colossal.Entities;
    using Colossal.IO.AssetDatabase;
    using Colossal.IO.AssetDatabase.VirtualTexturing;
    using Colossal.UI;

    using Game.Prefabs;
    using Game.Routes;
    using Game.UI.InGame;

    using BetterTransitSelector.Components;

    using cohtml.Net;

    using ModsCommon.Extensions;
    using ModsCommon.Systems;

    using Unity.Entities;
    using Unity.Mathematics;

    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.HighDefinition;

    using Color = UnityEngine.Color;

    #endregion

    /// <summary>
    /// The train builder's 3D view: the selected line's built train, laid out car by car in a private studio far
    /// below the map and filmed by a camera of its own into a texture the builder window shows.
    /// </summary>
    /// <remarks>
    /// <para>The train is drawn from its prefabs' meshes and materials, not from game entities: the game only draws
    /// entities the main camera can see, at the detail its distance calls for, so a hidden set of real trains
    /// would be culled or blurry.</para>
    /// <para>A clone of a game material loses its virtual-texture binding and draws black; every clone is bound
    /// to its texture stacks again (<see cref="TextureStreamingSystem.BindMaterial(Material,int,int,VTTextureParamBlock)"/>)
    /// and their tiles are requested while the view is open, as vanilla's VTTextureRequester does for batches.
    /// The studio set-up (private layers, local volume, sun switched off around this camera) follows PO2's
    /// material preview.</para>
    /// </remarks>
    public partial class BTS_ConsistPreviewSystem : CommonUISystemBase {
        protected override string ModId => Mod.Instance.Id;

        private const int kLayer        = 31;
        private const int kVolumeLayer  = 30;
        private const int kWidth        = 1024;
        private const int kHeight       = 400;
        private const float kFov        = 30f;
        private const float kMinPitch   = -5f;
        private const float kMaxPitch   = 80f;
        private const float kMinZoom    = 0.08f;
        private const float kMaxZoom    = 3f;
        private const float kExposure   = 2.5f;
        // Against the fixed exposure; a fill of 80 alone took a white body to near white.
        private const float kKeyLux     = 45f;
        private const float kFillLux    = 15f;

        /// <summary>Far below any map, so nothing of the city is near; the private layer is what isolates it.</summary>
        private static readonly Vector3 kStudio = new Vector3(0f, -100000f, 0f);

        // The UI keeps a cached image per URL, so every new texture gets a new name.
        private static int s_Generation;

        private SelectedInfoUISystem   m_SelectedInfo;
        private PrefabSystem           m_PrefabSystem;
        private TextureStreamingSystem m_Streaming;

        private BTS_ConsistBuilderSystem m_Builder;

        private ValueBindingHelper<string> m_Url;

        private bool m_Visible;
        private int  m_Signature;

        /// <summary>The line's colour, which transit vehicles wear in their externally-coloured channels.</summary>
        private Color? m_LineColor;

        // Studio.
        private GameObject    m_Root;
        private GameObject    m_Train;
        private Camera        m_Camera;
        private Light         m_Key;
        private Light         m_Fill;
        private VolumeProfile m_Profile;
        private RenderTexture m_Texture;
        private string        m_TextureName;
        private bool          m_CallbacksOn;
        private Light[]       m_Suppressed = Array.Empty<Light>();
        private bool[]        m_WasEnabled = Array.Empty<bool>();

        // Kept for the whole session and never given back. SurfaceAsset.Unload dropping the count to zero (the
        // game's batches hold none) destroys the surface's virtual-texture registration for everyone: its
        // VTAtlassingInfos go null, and it, and every later clone of it, draws a flat colour until a restart.
        private static readonly Dictionary<SurfaceAsset, Material> s_Materials = new Dictionary<SurfaceAsset, Material>();
        private static readonly Dictionary<RenderPrefab, Mesh[]>    s_Meshes    = new Dictionary<RenderPrefab, Mesh[]>();

        // Along the train (+z is the front): each car's front and back end.
        private readonly List<float2> m_CarSpans = new List<float2>();

        // Camera. The default is almost side-on with the front on the left, as the train reads in the editor.
        private const float kMissingLength = 20f;
        private const float kCarHalfWidth  = 1.6f;
        private const float kCarHeight     = 4.5f;
        private const float kDefaultYaw   = 180f;
        private const float kDefaultPitch = 6f;
        private float m_Yaw   = kDefaultYaw;
        private float m_Pitch = kDefaultPitch;
        private float m_Zoom  = 1f;

        /// <summary>Metres the view is slid along the train (+ towards the front), for trains longer than the view.</summary>
        private float m_Pan;
        private float m_PanRequest;

        /// <summary>A built car and where it is right now: cars glide to their places, and apart during a drag.</summary>
        private sealed class CarSlot {
            public readonly GameObject Object;
            public readonly float      Length;
            /// <summary>From the car's front coupling back to its model's origin.</summary>
            public readonly float      FrontToPivot;
            public float               Front;
            public float               Lift;
            public readonly List<MeshPart> Parts = new List<MeshPart>();

            public CarSlot(GameObject carObject, float length, float frontToPivot, float front) {
                Object       = carObject;
                Length       = length;
                FrontToPivot = frontToPivot;
                Front        = front;
            }
        }

        private readonly List<CarSlot> m_Cars = new List<CarSlot>();

        /// <summary>
        /// One renderer of a car, with what its per-slot blocks carry: livery, bones, and the highlight tint.
        /// As PO2 does, the per-slot blocks leave the shared cached materials unchanged.
        /// </summary>
        private sealed class MeshPart {
            private readonly MeshRenderer m_Renderer;
            private readonly int          m_Slots;
            private readonly Color[]      m_Livery;
            private readonly Skeleton     m_Skeleton;

            public MeshPart(MeshRenderer renderer, int slots, Color[] livery, Skeleton skeleton) {
                m_Renderer = renderer;
                m_Slots    = slots;
                m_Livery   = livery;
                m_Skeleton = skeleton;
            }

            /// <summary>
            /// Rebuilds the blocks with <paramref name="tint"/> on <c>_BaseColor</c>, which CS2's object shader
            /// multiplies the surface colour by (white leaves the car as the asset has it).
            /// </summary>
            public void Apply(Color tint) {
                for (var s = 0; s < m_Slots; s++) {
                    var block = new MaterialPropertyBlock();
                    if (m_Livery != null) {
                        block.SetColor(s_ColorMask0, m_Livery[0]);
                        block.SetColor(s_ColorMask1, m_Livery[1]);
                        block.SetColor(s_ColorMask2, m_Livery[2]);
                    }

                    block.SetColor(s_BaseColor, tint);
                    m_Skeleton?.Bind(block);
                    m_Renderer.SetPropertyBlock(block, s);
                }
            }
        }

        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        // Highlights drawn on the cars themselves: hover brightens, the selection (and a carried block) takes the
        // accent. Reapplied only when the state changes.
        private static readonly Color kHoverTint    = new Color(1.3f, 1.3f, 1.3f, 1f);
        private static readonly Color kSelectedTint = new Color(0.75f, 0.95f, 1.45f, 1f);
        private int m_Hover       = -1;
        private int m_SelectStart = -1;
        private int m_SelectCount;
        private int m_TintedHover = -2;
        private int m_TintedStart = -2;
        private int m_TintedCount = -2;
        private int m_TintedDrag  = -2;

        // Dragging a block of cars by hand: it lifts and follows the pointer along the track while the rest part
        // to show where it would go. The UI applies the move (consistMove) with the published gap on release.
        private const float kLift       = 0.8f;
        private const float kGlide      = 14f;
        private int   m_DragStart = -1;
        private int   m_DragCount;
        private float m_DragCentre;
        private float m_GrabOffset;
        private bool  m_Grabbed;
        private ValueBindingHelper<int> m_DropGap;

        // Animation: pantographs up while running. m_Posed is set once every arm has arrived.
        private readonly List<Skeleton> m_Skeletons = new List<Skeleton>();
        private bool  m_Running;
        private bool  m_Posed;
        private ValueBindingHelper<bool> m_RunningBinding;

        /// <summary>
        /// Where each car sits in the image, [x0, y0, x1, y1] per car in 0..1 (top-left origin), then each coupling
        /// as [x, y] (-1 when behind the camera): the UI lays its click, drag and drop zones over the picture.
        /// </summary>
        private ValueBindingHelper<float[]> m_Layout;
        private float[] m_LastLayout = Array.Empty<float>();
        private int   m_FocusStart;
        private int   m_FocusCount;

        protected override void OnCreate() {
            base.OnCreate();
            m_SelectedInfo = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_Builder      = World.GetOrCreateSystemManaged<BTS_ConsistBuilderSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_Streaming    = World.GetOrCreateSystemManaged<TextureStreamingSystem>();

            m_Url = CreateBinding("consistPreviewUrl", string.Empty);

            CreateTrigger<bool>("consistPreviewShow", show => {
                m_Visible = show;
                if (!show) {
                    TearDown();
                }
            });
            CreateTrigger<float, float>("consistPreviewOrbit", (dx, dy) => {
                m_Yaw   = (m_Yaw + dx * 0.4f) % 360f;
                m_Pitch = math.clamp(m_Pitch + dy * 0.3f, kMinPitch, kMaxPitch);
            });
            CreateTrigger<float>("consistPreviewZoom", steps => m_Zoom = math.clamp(m_Zoom * math.pow(1.15f, steps), kMinZoom, kMaxZoom));
            // (start, count) of the cars to frame; a count of 0 frames the whole train.
            CreateTrigger<int, int>("consistPreviewFocus", (start, count) => {
                if (start != m_FocusStart || count != m_FocusCount) {
                    m_FocusStart = start;
                    m_FocusCount = count;
                    m_Zoom       = 1f;
                }
            });
            CreateTrigger("consistPreviewReset", () => {
                m_Yaw   = kDefaultYaw;
                m_Pitch = kDefaultPitch;
                m_Zoom  = 1f;
                m_Pan   = 0f;
            });
            // A drag across the picture, as a fraction of its width: slides the view along the train.
            CreateTrigger<float>("consistPreviewPan", fraction => m_PanRequest += fraction);
            m_Layout = CreateBinding("consistPreviewLayout", Array.Empty<float>());
            // (start, count, x, y): the block being dragged and the pointer, in 0..1 of the picture (top-left origin).
            CreateTrigger<int, int, float, float>("consistPreviewDrag", (start, count, x, y) => {
                if (start != m_DragStart || count != m_DragCount) {
                    m_DragStart = start;
                    m_DragCount = count;
                    m_Grabbed   = false;
                }

                if (!PointerOnTrack(x, y, out var z)) {
                    return;
                }

                // Held where it was grabbed, so the block doesn't jump to put its centre under the pointer.
                if (!m_Grabbed && TryBlock(out var front, out var length)) {
                    m_GrabOffset = front - length * 0.5f - z;
                    m_Grabbed    = true;
                }

                m_DragCentre = z + m_GrabOffset;
            });
            CreateTrigger("consistPreviewDragEnd", EndDrag);
            // (hover, selection start, selection count); -1 / 0 for none.
            CreateTrigger<int, int, int>("consistPreviewHighlight", (hover, start, count) => {
                m_Hover       = hover;
                m_SelectStart = start;
                m_SelectCount = count;
            });
            m_DropGap = CreateBinding("consistPreviewDropGap", -1);
            m_RunningBinding = CreateBinding("consistPreviewRunning", false);
            CreateTrigger<bool>("consistPreviewRun", run => {
                m_Running              = run;
                m_Posed                = false;
                m_RunningBinding.Value = run;
            });

            var images = UIManager.defaultUISystem?.userImagesManager;
            if (images != null) {
                images.AllocateUserImageData += OnAllocateImage;
            }
        }

        protected override void OnDestroy() {
            var images = UIManager.defaultUISystem?.userImagesManager;
            if (images != null) {
                images.AllocateUserImageData -= OnAllocateImage;
            }

            TearDown();
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            base.OnUpdate();
            if (!m_Visible) {
                return;
            }

            var line = SelectedLine();
            // The builder's draft, not the line's train: the preview shows what is being edited.
            var cars = m_Builder.DraftCars;
            m_LineColor = EntityManager.TryGetComponent(line, out global::Game.Routes.Color lineColor) ? (Color)lineColor.m_Color : null;
            var signature = unchecked(Signature(line, cars) * 31 + (m_LineColor?.GetHashCode() ?? 0));
            if (signature != m_Signature || m_Root == null) {
                m_Signature = signature;
                m_Posed     = false;
                EndDrag();
                try {
                    Build(cars);
                } catch (Exception e) {
                    m_Log.Error($"Preview build failed: {e}");
                    TearDown();
                    return;
                }
            }

            // No CPU tile requests: vanilla makes none for ordinary meshes either; the camera's GPU feedback asks
            // for what it draws. Region requests every frame flooded the shared tile cache.
            LayoutCars();
            Highlight();
            PlaceCamera();
            Animate();
        }

        private void Highlight() {
            var drag = Dragging ? m_DragStart * 1000 + m_DragCount : -1;
            if (m_Hover == m_TintedHover && m_SelectStart == m_TintedStart && m_SelectCount == m_TintedCount && drag == m_TintedDrag) {
                return;
            }

            m_TintedHover = m_Hover;
            m_TintedStart = m_SelectStart;
            m_TintedCount = m_SelectCount;
            m_TintedDrag  = drag;
            for (var i = 0; i < m_Cars.Count; i++) {
                var selected = (i >= m_SelectStart && i < m_SelectStart + m_SelectCount)
                               || (Dragging && i >= m_DragStart && i < m_DragStart + m_DragCount);
                var tint = selected ? kSelectedTint : i == m_Hover ? kHoverTint : Color.white;
                if (selected && i == m_Hover) {
                    tint *= 1.15f;
                    tint.a = 1f;
                }

                foreach (var part in m_Cars[i].Parts) {
                    part.Apply(tint);
                }
            }
        }

        private void EndDrag() {
            m_DragStart = -1;
            m_DragCount = 0;
            m_Grabbed   = false;
            if (m_DropGap != null && m_DropGap.Value != -1) {
                m_DropGap.Value = -1;
            }
        }

        private bool Dragging => m_DragStart >= 0 && m_DragCount > 0 && m_DragStart + m_DragCount <= m_Cars.Count;

        /// <summary>The dragged block's resting front and length.</summary>
        private bool TryBlock(out float front, out float length) {
            front  = 0f;
            length = 0f;
            if (!Dragging) {
                return false;
            }

            front = m_CarSpans[m_DragStart].x;
            for (var i = m_DragStart; i < m_DragStart + m_DragCount; i++) {
                length += m_Cars[i].Length;
            }

            return true;
        }

        /// <summary>
        /// Where the pointer is along the track: its ray through the camera meets the upright plane that holds the
        /// train's centre line and faces the camera. False when the camera looks along the train.
        /// </summary>
        private bool PointerOnTrack(float x, float y, out float z) {
            z = 0f;
            if (m_Camera == null || m_Root == null) {
                return false;
            }

            var ray       = m_Camera.ViewportPointToRay(new Vector3(x, 1f - y, 0f));
            var origin    = m_Root.transform.InverseTransformPoint(ray.origin);
            var direction = m_Root.transform.InverseTransformDirection(ray.direction);
            var normal    = new Vector3(direction.x, direction.y, 0f);
            if (normal.sqrMagnitude < 0.01f) {
                return false;
            }

            normal.Normalize();
            var denominator = Vector3.Dot(normal, direction);
            if (Mathf.Abs(denominator) < 0.001f) {
                return false;
            }

            var t = Vector3.Dot(normal, new Vector3(0f, kCarHeight * 0.5f, 0f) - origin) / denominator;
            z = (origin + direction * t).z;
            return true;
        }

        /// <summary>
        /// Glides every car towards its place. During a drag the block follows the pointer, lifted, and the others
        /// close up around it with room left where it would drop; that position is published for the UI's move.
        /// </summary>
        private void LayoutCars() {
            var count  = m_Cars.Count;
            var target = new float[count];
            var lift   = new float[count];
            if (TryBlock(out _, out var blockLength)) {
                // The cars left behind, in order, and which of them the block would go in front of.
                var gap    = 0;
                var cursor = 0f;
                for (var i = 0; i < count; i++) {
                    if (i >= m_DragStart && i < m_DragStart + m_DragCount) {
                        continue;
                    }

                    if (cursor - m_Cars[i].Length * 0.5f > m_DragCentre) {
                        gap++;
                    }

                    cursor -= m_Cars[i].Length;
                }

                cursor = 0f;
                var placed = 0;
                for (var i = 0; i < count; i++) {
                    if (i >= m_DragStart && i < m_DragStart + m_DragCount) {
                        continue;
                    }

                    if (placed == gap) {
                        cursor -= blockLength;
                    }

                    target[i] = cursor;
                    cursor   -= m_Cars[i].Length;
                    placed++;
                }

                var blockFront = m_DragCentre + blockLength * 0.5f;
                for (var i = m_DragStart; i < m_DragStart + m_DragCount; i++) {
                    target[i]   = blockFront;
                    lift[i]     = kLift;
                    blockFront -= m_Cars[i].Length;
                }

                if (m_DropGap.Value != gap) {
                    m_DropGap.Value = gap;
                }
            } else {
                for (var i = 0; i < count; i++) {
                    target[i] = m_CarSpans[i].x;
                }
            }

            var blend = Mathf.Min(1f, UnityEngine.Time.deltaTime * kGlide);
            for (var i = 0; i < count; i++) {
                var car = m_Cars[i];
                car.Front += (target[i] - car.Front) * blend;
                car.Lift  += (lift[i] - car.Lift) * blend;
                car.Object.transform.localPosition = new Vector3(0f, car.Lift, car.Front - car.FrontToPivot);
            }
        }

        #region Train

        private Entity SelectedLine() {
            var line = m_SelectedInfo.selectedEntity;
            var em   = EntityManager;
            return line == Entity.Null || !em.Exists(line) || !em.HasComponent<TransportLine>(line) ? Entity.Null : line;
        }

        private static int Signature(Entity line, IReadOnlyList<BTS_ConsistCar> cars) {
            unchecked {
                var hash = line.Index * 31 + line.Version;
                for (var i = 0; i < cars.Count; i++) {
                    hash = hash * 31 + cars[i].m_Prefab.Index * 2 + (cars[i].m_Reversed ? 1 : 0);
                }

                return hash;
            }
        }

        /// <summary>Lays the train out from the front (z = 0) backwards, each car as the game couples it.</summary>
        private void Build(IReadOnlyList<BTS_ConsistCar> cars) {
            EnsureStudio();
            ClearTrain();

            m_Train = new GameObject("BTS Consist Preview Train") { hideFlags = HideFlags.HideAndDontSave, layer = kLayer };
            m_Train.transform.SetParent(m_Root.transform, false);

            // Copied first: building loads assets, and the draft must not change under it.
            var list = new List<BTS_ConsistCar>(cars);

            var em     = EntityManager;
            var cursor = 0f;
            foreach (var car in list) {
                var carObject = new GameObject("Car") { hideFlags = HideFlags.HideAndDontSave, layer = kLayer };
                carObject.transform.SetParent(m_Train.transform, false);

                if (!em.TryGetComponent(car.m_Prefab, out TrainData train)) {
                    // A missing asset still takes room, as in the editor, so it can be selected and removed.
                    m_CarSpans.Add(new float2(cursor, cursor - kMissingLength));
                    m_Cars.Add(new CarSlot(carObject, kMissingLength, 0f, cursor));
                    cursor -= kMissingLength;
                    continue;
                }

                // VehicleUtils: a reversed car swaps its coupling offsets; its length is their sum.
                var offsets = car.m_Reversed ? train.m_AttachOffsets.yx : train.m_AttachOffsets;
                var length  = math.csum(offsets);
                m_CarSpans.Add(new float2(cursor, cursor - length));
                m_Cars.Add(new CarSlot(carObject, length, offsets.x, cursor));
                cursor -= length;

                carObject.transform.localPosition = new Vector3(0f, 0f, cursor + length - offsets.x);
                carObject.transform.localRotation = car.m_Reversed ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

                AddMeshes(m_Cars[m_Cars.Count - 1], car.m_Prefab);
            }

            m_TintedHover = -2;
        }

        private void AddMeshes(CarSlot slot, Entity prefab) {
            var carObject = slot.Object;
            var em = EntityManager;
            if (!em.TryGetBuffer(prefab, true, out DynamicBuffer<SubMesh> subMeshes)) {
                return;
            }

            // Copied: loading meshes and materials below can touch the prefab world.
            var parts = new List<SubMesh>(subMeshes.Length);
            for (var i = 0; i < subMeshes.Length; i++) {
                parts.Add(subMeshes[i]);
            }

            foreach (var part in parts) {
                if (!Shown(part.m_Flags)
                    || !em.TryGetComponent(part.m_SubMesh, out PrefabData data)
                    || !m_PrefabSystem.TryGetPrefab(data, out RenderPrefab render)) {
                    continue;
                }

                if (!s_Meshes.TryGetValue(render, out var meshes)) {
                    meshes = render.ObtainMeshes();
                    s_Meshes.Add(render, meshes);
                }

                if (meshes == null) {
                    continue;
                }

                var colors   = LiveryOf(render);
                var skeleton = Skeleton.For(render);
                if (skeleton != null) {
                    m_Skeletons.Add(skeleton);
                }

                // A geometry's meshes share its surfaces in order: mesh k draws surfaces [offset, offset + its submeshes).
                var offset = 0;
                foreach (var mesh in meshes) {
                    if (mesh == null) {
                        continue;
                    }

                    var materials = new Material[mesh.subMeshCount];
                    for (var s = 0; s < materials.Length; s++) {
                        materials[s] = offset + s < render.materialCount ? MaterialOf(render.GetSurfaceAsset(offset + s)) : null;
                    }

                    offset += mesh.subMeshCount;

                    var partObject = new GameObject("Mesh") { hideFlags = HideFlags.HideAndDontSave, layer = kLayer };
                    partObject.transform.SetParent(carObject.transform, false);
                    partObject.transform.localPosition = part.m_Position;
                    partObject.transform.localRotation = part.m_Rotation;
                    partObject.AddComponent<MeshFilter>().sharedMesh = mesh;

                    var renderer = partObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials   = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows    = false;

                    var meshPart = new MeshPart(renderer, materials.Length, colors, skeleton);
                    slot.Parts.Add(meshPart);
                    meshPart.Apply(Color.white);

                    // Without the keyword the shader ignores the bones. Per surface: a surface belongs to one mesh.
                    if (skeleton != null) {
                        foreach (var material in materials) {
                            material?.EnableKeyword("_GPU_ANIMATION_PROCEDURAL");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The livery: a mesh's three mask colours, which the game gives each vehicle from its colour variations
        /// (a white-and-yellow train is a white texture with yellow in a mask channel). Without them every
        /// channel is white. The first variation is shown, as vanilla's RenderPrefabRenderer does by default, and a
        /// channel the asset lets be coloured from outside wears the line's colour, as a running train does.
        /// </summary>
        private Color[] LiveryOf(RenderPrefab render) {
            if (!render.TryGet(out ColorProperties colors) || !colors.active || colors.m_ColorVariations.Count == 0) {
                return null;
            }

            var livery = new Color[3];
            for (sbyte channel = 0; channel < 3; channel++) {
                var external = m_LineColor.HasValue
                               && channel < colors.m_ChannelsBinding.Count
                               && colors.m_ChannelsBinding[channel].m_CanBeModifiedByExternal;
                livery[channel] = external ? m_LineColor.Value : colors.GetColor(0, channel);
            }

            return livery;
        }

        private static readonly int s_ColorMask0 = Shader.PropertyToID("colossal_ColorMask0");
        private static readonly int s_ColorMask1 = Shader.PropertyToID("colossal_ColorMask1");
        private static readonly int s_ColorMask2 = Shader.PropertyToID("colossal_ColorMask2");

        /// <summary>
        /// Which variants of a car to draw: the right-hand-traffic, forward-running, loaded, non-editor one. The
        /// rest are alternatives the game picks between per instance.
        /// </summary>
        private static bool Shown(SubMeshFlags flags) =>
            (flags & (SubMeshFlags.RequireEditor
                      | SubMeshFlags.RequireLeftHandTraffic
                      | SubMeshFlags.RequireBackward
                      | SubMeshFlags.RequireEmpty
                      | SubMeshFlags.RequirePartial1
                      | SubMeshFlags.RequirePartial2
                      | SubMeshFlags.OutlineOnly)) == 0;

        /// <summary>One bound clone per surface, however many cars use it (PO2's native-surface path).</summary>
        private Material MaterialOf(SurfaceAsset surface) {
            if (surface == null) {
                return null;
            }

            if (s_Materials.TryGetValue(surface, out var cached)) {
                return cached;
            }

            // The reference Load takes is kept for the session (see s_Materials): never Unload.
            var source = surface.Load(-1, true, TextureAsset.KeepOnCPU.Dont, true);
            var clone  = source != null ? new Material(source) { name = $"BTS Preview {surface.name}", hideFlags = HideFlags.HideAndDontSave } : null;
            s_Materials.Add(surface, clone);

            // Only real atlassing: pre-reserved infos point at a slot with no texture data behind it.
            if (clone != null && surface.isVTMaterial) {
                var infos = surface.VTAtlassingInfos;
                if (infos != null) {
                    clone.EnableKeyword("ENABLE_VT");
                    for (var stack = 0; stack < Math.Min(infos.Length, 2); stack++) {
                        var info = infos[stack];
                        if (info.indexInStack < 0) {
                            continue;
                        }

                        m_Streaming.BindMaterial(clone, info.stackGlobalIndex, stack, m_Streaming.GetTextureParamBlock(info));
                    }
                }
            }

            return clone;
        }

        private void ClearTrain() {
            if (m_Train != null) {
                UnityEngine.Object.Destroy(m_Train);
                m_Train = null;
            }

            // Materials and meshes stay cached: giving them back is what broke the surfaces (see s_Materials).
            m_CarSpans.Clear();
            m_Cars.Clear();

            foreach (var skeleton in m_Skeletons) {
                skeleton.Dispose();
            }

            m_Skeletons.Clear();
        }

        /// <summary>
        /// Raises the pantographs while running and lowers them when stopped, at each arm's own speed. CS2 trains
        /// have no wheel bones (wheels are fixed in the bogies), so the pantograph is what a running train shows.
        /// Posed every frame until every arm has arrived.
        /// </summary>
        private void Animate() {
            if (m_Skeletons.Count == 0 || m_Posed) {
                return;
            }

            var settled = true;
            foreach (var skeleton in m_Skeletons) {
                settled &= skeleton.Pose(m_Running, UnityEngine.Time.deltaTime);
            }

            m_Posed = settled;
        }

        /// <summary>
        /// One animated mesh's bones, fed to the shader as vanilla's RenderPrefabRenderer does: a raw buffer of
        /// skin matrices (this frame's, then last frame's for motion vectors) behind <c>_BoneTransforms</c>.
        /// Pantograph arms follow ObjectInterpolateSystem.AnimatePantographBone with no wire in reach; every
        /// other bone keeps its rest pose.
        /// </summary>
        private sealed class Skeleton : IDisposable {
            private readonly ProceduralAnimationProperties.BoneInfo[] m_Bones;
            private readonly float[]                                  m_Angle;
            private readonly Matrix4x4[]                              m_Model;
            private readonly Matrix4x4[]                              m_Skin;
            private readonly ComputeBuffer                            m_Buffer;

            private Skeleton(ProceduralAnimationProperties.BoneInfo[] bones) {
                m_Bones  = bones;
                m_Angle  = new float[bones.Length];
                m_Model  = new Matrix4x4[bones.Length];
                m_Skin   = new Matrix4x4[bones.Length * 2];
                m_Buffer = new ComputeBuffer(bones.Length * 2, 64, ComputeBufferType.Raw);
                Pose(false, 0f);
            }

            public static Skeleton For(RenderPrefab render) =>
                render.TryGet(out ProceduralAnimationProperties animation)
                && animation.active
                && animation.m_Bones != null
                && animation.m_Bones.Length > 0
                    ? new Skeleton(animation.m_Bones)
                    : null;

            public void Bind(MaterialPropertyBlock block) {
                block.SetBuffer(s_BoneTransforms, m_Buffer);
                block.SetVector(s_BoneParameters, new Vector2(0f, m_Bones.Length));
                block.SetInt(s_BonePreviousOffset, m_Bones.Length * 64);
            }

            /// <summary>Moves every arm one step towards up or down; true once all have arrived.</summary>
            public bool Pose(bool raised, float deltaTime) {
                var settled = true;
                for (var i = 0; i < m_Bones.Length; i++) {
                    if (!IsLowerArm(i)) {
                        continue;
                    }

                    var target = raised ? RaisedAngle(i) : 0f;
                    var step   = Mathf.Max(0.5f, m_Bones[i].m_Speed * Mathf.PI * 2f) * deltaTime;
                    m_Angle[i] = Mathf.MoveTowards(m_Angle[i], target, deltaTime > 0f ? step : float.MaxValue);
                    settled   &= Mathf.Approximately(m_Angle[i], target);
                }

                // The previous frame's pose moves to the back half, as the shader expects.
                Array.Copy(m_Skin, 0, m_Skin, m_Bones.Length, m_Bones.Length);
                for (var i = 0; i < m_Bones.Length; i++) {
                    var bone  = m_Bones[i];
                    var local = Matrix4x4.TRS(bone.position, RotationOf(i), bone.scale);
                    // Parents come before their children in CS2's bone arrays; a later parent falls back to rest.
                    m_Model[i] = bone.parentId >= 0 && bone.parentId < i ? m_Model[bone.parentId] * local : local;
                    m_Skin[i]  = m_Model[i] * bone.bindPose;
                }

                m_Buffer.SetData(m_Skin);
                return settled;
            }

            private Quaternion RotationOf(int i) {
                var bone = m_Bones[i];
                if (IsLowerArm(i)) {
                    return bone.rotation * Quaternion.Euler(m_Angle[i] * Mathf.Rad2Deg, 0f, 0f);
                }

                // The upper arm turns back twice as far as the lower one, keeping the head level.
                if (bone.m_Type == BoneType.PantographRotation && IsLowerArm(bone.parentId)) {
                    return Quaternion.Euler(-2f * m_Angle[bone.parentId] * Mathf.Rad2Deg, 0f, 0f) * bone.rotation;
                }

                return bone.rotation;
            }

            private bool IsLowerArm(int i) =>
                i >= 0
                && m_Bones[i].m_Type == BoneType.PantographRotation
                && (m_Bones[i].parentId < 0 || m_Bones[m_Bones[i].parentId].m_Type != BoneType.PantographRotation);

            /// <summary>
            /// Vanilla with no wire found: the head at 0.3827 of the arms' reach (22.5°), turned away from where
            /// the upper arm points.
            /// </summary>
            private float RaisedAngle(int i) {
                var child = ChildOf(i);
                if (child < 0) {
                    return 0f;
                }

                var reach      = new Vector2(m_Bones[child].position.y, m_Bones[child].position.z).magnitude;
                var grandchild = m_Bones[child].m_Type == BoneType.PantographRotation ? ChildOf(child) : -1;
                if (grandchild >= 0) {
                    reach += new Vector2(m_Bones[grandchild].position.y, m_Bones[grandchild].position.z).magnitude;
                }

                var angle = Mathf.Asin(Mathf.Min(0.9f, reach * 0.38268343f / Mathf.Max(reach, 0.001f)));
                return m_Bones[child].position.z > 0f ? -angle : angle;
            }

            private int ChildOf(int i) {
                for (var j = 0; j < m_Bones.Length; j++) {
                    if (m_Bones[j].parentId == i) {
                        return j;
                    }
                }

                return -1;
            }

            public void Dispose() => m_Buffer.Release();
        }

        private static readonly int s_BoneTransforms     = Shader.PropertyToID("_BoneTransforms");
        private static readonly int s_BoneParameters     = Shader.PropertyToID("colossal_BoneParameters");
        private static readonly int s_BonePreviousOffset = Shader.PropertyToID("_BonePreviousTransformsByteOffset");

        #endregion

        #region Studio

        private void EnsureStudio() {
            if (m_Root != null) {
                return;
            }

            m_Root = new GameObject("BTS Consist Preview") { hideFlags = HideFlags.HideAndDontSave, layer = kLayer };
            m_Root.transform.position = kStudio;

            // A local volume for this camera alone: fixed exposure, and no sky or ambient from the city's.
            var volumeObject = new GameObject("Volume") { hideFlags = HideFlags.HideAndDontSave, layer = kVolumeLayer };
            volumeObject.transform.SetParent(m_Root.transform, false);
            var bounds = volumeObject.AddComponent<BoxCollider>();
            bounds.isTrigger = true;
            bounds.size      = new Vector3(20000f, 20000f, 20000f);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 10000f;
            m_Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_Profile.hideFlags = HideFlags.HideAndDontSave;
            volume.sharedProfile = m_Profile;
            var exposure = m_Profile.Add<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(kExposure);
            var indirect = m_Profile.Add<IndirectLightingController>();
            indirect.indirectDiffuseLightingMultiplier.Override(0.6f);
            indirect.reflectionLightingMultiplier.Override(1f);

            // A sky of our own, for ambient light and reflections only (the background stays the clear colour):
            // without anything to reflect, glass reads as body colour and a train looks untextured. A dark
            // ground under a light sky gives windows their dark lower half, as in the city.
            var environment = m_Profile.Add<VisualEnvironment>();
            environment.skyType.Override((int)SkyType.Gradient);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            var sky = m_Profile.Add<GradientSky>();
            sky.top.Override(new Color(0.55f, 0.65f, 0.8f, 1f));
            sky.middle.Override(new Color(0.75f, 0.78f, 0.82f, 1f));
            sky.bottom.Override(new Color(0.12f, 0.12f, 0.13f, 1f));

            var cameraObject = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave, layer = kLayer };
            cameraObject.transform.SetParent(m_Root.transform, false);
            m_Camera                 = cameraObject.AddComponent<Camera>();
            m_Camera.cullingMask     = 1 << kLayer;
            m_Camera.clearFlags      = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = new Color(0.05f, 0.055f, 0.065f, 1f);
            m_Camera.fieldOfView     = kFov;
            m_Camera.nearClipPlane   = 0.3f;
            m_Camera.farClipPlane    = 3000f;
            var cameraData = cameraObject.AddComponent<HDAdditionalCameraData>();
            cameraData.volumeLayerMask    = 1 << kVolumeLayer;
            cameraData.clearColorMode     = HDAdditionalCameraData.ClearColorMode.Color;
            cameraData.backgroundColorHDR = m_Camera.backgroundColor;
            cameraData.antialiasing       = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            // The lights ride on the camera, so whatever side it orbits to is the lit side: a key from over the
            // viewer's left shoulder, a fill from the right. Fixed lights left the far side lit and the view
            // flat grey. Both are on only while this camera renders (see OnBeginRender).
            m_Key  = CreateLight("Key", Quaternion.Euler(35f, 25f, 0f), kKeyLux);
            m_Fill = CreateLight("Fill", Quaternion.Euler(10f, -50f, 0f), kFillLux);

            m_TextureName = $"BTS_ConsistPreview_{++s_Generation}";
            m_Texture = new RenderTexture(kWidth, kHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) {
                name      = m_TextureName,
                hideFlags = HideFlags.HideAndDontSave,
            };
            m_Texture.Create();
            m_Camera.targetTexture = m_Texture;

            RenderPipelineManager.beginCameraRendering += OnBeginRender;
            RenderPipelineManager.endCameraRendering   += OnEndRender;
            m_CallbacksOn = true;

            // Served by OnAllocateImage; the name is new per texture, so the UI never shows a stale one.
            m_Url.Value = $"live://bts/{m_TextureName}";
        }

        private Light CreateLight(string name, Quaternion rotation, float lux) {
            var lightObject = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = kLayer };
            lightObject.transform.SetParent(m_Camera.transform, false);
            lightObject.transform.localRotation = rotation;
            var light = lightObject.AddComponent<Light>();
            light.type        = LightType.Directional;
            light.color       = Color.white;
            light.intensity   = lux;
            light.shadows     = LightShadows.None;
            light.cullingMask = 1 << kLayer;
            light.enabled     = false;
            return light;
        }

        /// <summary>Frames the focused cars from the orbit angles.</summary>
        private void PlaceCamera() {
            if (m_Camera == null) {
                return;
            }

            var front = 0f;
            var back  = 0f;
            if (m_CarSpans.Count > 0) {
                var start = m_FocusCount > 0 ? math.clamp(m_FocusStart, 0, m_CarSpans.Count - 1) : 0;
                var end   = m_FocusCount > 0 ? math.clamp(m_FocusStart + m_FocusCount, start + 1, m_CarSpans.Count) : m_CarSpans.Count;
                front = m_CarSpans[start].x;
                back  = m_CarSpans[end - 1].y;
            }

            // Wide enough for the focused length across the image, with a margin; never closer than a car's height.
            var length   = math.max(front - back, 6f);
            var aspect   = (float)kWidth / kHeight;
            var hHalf    = math.atan(math.tan(math.radians(kFov) * 0.5f) * aspect);
            var fit      = (length * 0.5f + 4f) / math.tan(hHalf);
            var distance = math.max(fit * m_Zoom, 8f);
            var orbit    = Quaternion.Euler(m_Pitch, m_Yaw - 90f, 0f);

            // A drag slides the train under the view by as many metres as the picture's width covers there; how
            // far along the train that is depends on how the camera faces it.
            var viewWidth = 2f * distance * math.tan(hHalf);
            var along     = -(orbit * Vector3.right).z;
            m_Pan        += m_PanRequest * viewWidth * (math.abs(along) > 0.2f ? math.sign(along) : 0f);
            m_PanRequest  = 0f;
            var half      = (front - back) * 0.5f;
            m_Pan         = math.clamp(m_Pan, -half, half);

            var target  = new Vector3(0f, 2.2f, (front + back) * 0.5f + m_Pan);
            var offset  = orbit * new Vector3(0f, 0f, -distance);

            var transform = m_Camera.transform;
            transform.localPosition = target + offset;
            transform.localRotation = orbit;
            m_Camera.farClipPlane   = math.max(3000f, fit * 4f);
            m_Camera.enabled        = true;

            PublishLayout();
        }

        /// <summary>
        /// Projects each car's box through the camera and publishes where it lands in the picture, when that moved.
        /// A box behind the camera is sent as all zeros (nothing to click).
        /// </summary>
        private void PublishLayout() {
            // Cars first, then the couplings: gap g (in front of car g; the last is the tail) as [x, y], at mid height.
            // Live positions, so the zones follow cars that are gliding or being dragged.
            var cars   = m_Cars.Count;
            var layout = new float[cars * 4 + (cars + 1) * 2];
            for (var g = 0; g <= cars; g++) {
                var z    = g < cars ? m_Cars[g].Front : cars > 0 ? m_Cars[cars - 1].Front - m_Cars[cars - 1].Length : 0f;
                var view = m_Camera.WorldToViewportPoint(m_Root.transform.TransformPoint(new Vector3(0f, kCarHeight * 0.5f, z)));
                layout[cars * 4 + g * 2]     = view.z > 0f ? view.x : -1f;
                layout[cars * 4 + g * 2 + 1] = view.z > 0f ? 1f - view.y : -1f;
            }

            for (var i = 0; i < cars; i++) {
                var span = new float2(m_Cars[i].Front, m_Cars[i].Front - m_Cars[i].Length);
                var rise = m_Cars[i].Lift;
                var min  = new float2(float.MaxValue);
                var max  = new float2(float.MinValue);
                var seen = true;
                for (var corner = 0; corner < 8 && seen; corner++) {
                    var local = new Vector3(
                        (corner & 1) == 0 ? -kCarHalfWidth : kCarHalfWidth,
                        rise + ((corner & 2) == 0 ? 0f : kCarHeight),
                        (corner & 4) == 0 ? span.x : span.y);
                    var view = m_Camera.WorldToViewportPoint(m_Root.transform.TransformPoint(local));
                    seen &= view.z > 0f;
                    var point = new float2(view.x, 1f - view.y);
                    min = math.min(min, point);
                    max = math.max(max, point);
                }

                if (seen) {
                    layout[i * 4]     = min.x;
                    layout[i * 4 + 1] = min.y;
                    layout[i * 4 + 2] = max.x;
                    layout[i * 4 + 3] = max.y;
                }
            }

            if (!Moved(layout, m_LastLayout)) {
                return;
            }

            m_LastLayout   = layout;
            m_Layout.Value = layout;
        }

        private static bool Moved(float[] a, float[] b) {
            if (a.Length != b.Length) {
                return true;
            }

            for (var i = 0; i < a.Length; i++) {
                if (math.abs(a[i] - b[i]) > 0.0005f) {
                    return true;
                }
            }

            return false;
        }

        private const string kInfoviewKeyword = "INFOVIEW_ON";
        private static readonly int s_InfoviewOn = Shader.PropertyToID("colossal_InfoviewOn");
        private int  m_InfoviewWas;
        private bool m_InfoviewKeywordWas;

        // CS2's sun, moon and night lights ignore this camera's culling mask; they are switched off only while it
        // renders, and our own lights are switched on only then (a directional light would light the city).
        private void OnBeginRender(ScriptableRenderContext context, Camera camera) {
            if (camera != m_Camera) {
                return;
            }

            if (m_Suppressed.Length == 0) {
                m_Suppressed = new[] { TaggedLight("SunLight"), TaggedLight("MoonLight"), TaggedLight("NightLight") };
                m_WasEnabled = new bool[m_Suppressed.Length];
            }

            for (var i = 0; i < m_Suppressed.Length; i++) {
                var light = m_Suppressed[i];
                m_WasEnabled[i] = light != null && light.enabled;
                if (m_WasEnabled[i]) {
                    light.enabled = false;
                }
            }

            m_Key.enabled  = true;
            m_Fill.enabled = true;

            // An open info view (the transport one is, whenever a line is selected) greys every object through
            // a global switch, and this camera would inherit it: the train came out flat 0.47 grey, looking
            // untextured. Off for this camera only, as vanilla's ThumbnailCustomPass does for thumbnails.
            m_InfoviewWas        = Shader.GetGlobalInt(s_InfoviewOn);
            m_InfoviewKeywordWas = Shader.IsKeywordEnabled(kInfoviewKeyword);
            Shader.SetGlobalInt(s_InfoviewOn, 0);
            Shader.DisableKeyword(kInfoviewKeyword);
        }

        private void OnEndRender(ScriptableRenderContext context, Camera camera) {
            if (camera != m_Camera) {
                return;
            }

            m_Key.enabled  = false;
            m_Fill.enabled = false;
            Shader.SetGlobalInt(s_InfoviewOn, m_InfoviewWas);
            if (m_InfoviewKeywordWas) {
                Shader.EnableKeyword(kInfoviewKeyword);
            }

            for (var i = 0; i < m_Suppressed.Length; i++) {
                if (m_WasEnabled[i] && m_Suppressed[i] != null) {
                    m_Suppressed[i].enabled = true;
                }
            }
        }

        private static Light TaggedLight(string tag) {
            try {
                var found = GameObject.FindGameObjectWithTag(tag);
                return found != null ? found.GetComponent<Light>() : null;
            } catch (UnityException) {
                // An undefined tag throws rather than returning null.
                return null;
            }
        }

        private ResourceResponse.UserImageData OnAllocateImage(string name, int width, int height) {
            if (m_Texture == null || m_TextureName == null || name == null || !name.Contains(m_TextureName)) {
                return null;
            }

            // Dynamic: Gameface reads the texture every frame instead of copying it once.
            return UIManager.defaultUISystem.userImagesManager.GetUserImageData(m_Texture, UserImagesManager.ResourceType.Unmanaged, true);
        }

        private void TearDown() {
            ClearTrain();

            if (m_CallbacksOn) {
                RenderPipelineManager.beginCameraRendering -= OnBeginRender;
                RenderPipelineManager.endCameraRendering   -= OnEndRender;
                m_CallbacksOn = false;
            }

            // Gameface holds its own registration of the texture; drop it before the texture goes.
            if (m_Texture != null) {
                try {
                    UIManager.defaultUISystem?.userImagesManager?.RemoveUserImageData(m_Texture);
                } catch (Exception e) {
                    m_Log.Warn($"Removing the preview image failed: {e.Message}");
                }

                if (m_Camera != null) {
                    m_Camera.targetTexture = null;
                }

                m_Texture.Release();
                UnityEngine.Object.Destroy(m_Texture);
                m_Texture = null;
            }

            if (m_Profile != null) {
                UnityEngine.Object.Destroy(m_Profile);
                m_Profile = null;
            }

            if (m_Root != null) {
                UnityEngine.Object.Destroy(m_Root);
                m_Root = null;
            }

            m_Camera     = null;
            m_Key        = null;
            m_Fill       = null;
            m_Suppressed = Array.Empty<Light>();
            m_Signature  = 0;
            if (m_LastLayout.Length > 0) {
                m_LastLayout   = Array.Empty<float>();
                m_Layout.Value = m_LastLayout;
            }
            if (m_Url != null && m_Url.Value != string.Empty) {
                m_Url.Value = string.Empty;
            }
        }

        #endregion
    }
}
