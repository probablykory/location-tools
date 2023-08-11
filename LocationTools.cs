using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

#region Blueprint & LocationTool helpers

[ScriptedImporter(1, "blueprint")]
public class BlueprintImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        TextAsset subAsset = new TextAsset(File.ReadAllText(ctx.assetPath));
        ctx.AddObjectToAsset("text", subAsset);
        ctx.SetMainObject(subAsset);
    }
}

public static class LocationToolExtensionMethods
{
    public static List<string> ToList(this TextAsset textAsset)
    {
        return new List<string>(textAsset.text.Split(
            new string[] { "\r\n", "\r", "\n" },
            StringSplitOptions.None
        ));
    }

    public static bool HasComponent(this GameObject go, Type type)
    {
        var comp = go.GetComponent(type);
        return comp != null;
    }


    private static MethodInfo moveImpl = null;
    public static bool MoveToGameObject(this Component c, GameObject go)
    {
        if (moveImpl == null)
        {
            moveImpl = typeof(ComponentUtility).GetMethod(
                "MoveComponentToGameObject",
                BindingFlags.NonPublic | BindingFlags.Static,
                Type.DefaultBinder,
                new[] { typeof(Component), typeof(GameObject) },
                null);
        }
        if (moveImpl != null)
        {
            return (bool)moveImpl.Invoke(null, new object[] { c, go });
        }

        return false;
    }
}

[Serializable]
public struct BlueprintPrefab
{
    public string prefabName;
    public string type;
    public float posX;
    public float posY;
    public float posZ;
    public float rotW;
    public float rotX;
    public float rotY;
    public float rotZ;

    public static bool IsDefault<TValue>(TValue value) =>
        EqualityComparer<TValue>.Default.Equals(value, default(TValue));
}


#endregion

public class LocationToolsWindow : EditorWindow
{
    private GUIStyle foldoutBoldStyle = null;
    private GUIStyle greyLabelStyle = null;
    private double renameTime;

    [SerializeField]
    private Vector2 m_ScrollPosition;

    [MenuItem("Tools/Location Tools")]
    static void ShowWindow()
    {
        // Get existing open window or if none, make a new one:
        var window = GetWindow<LocationToolsWindow>();
        window.titleContent = new GUIContent("Location Tools");
        window.Show();

        // Foldout style with bold font
        window.foldoutBoldStyle = new GUIStyle(EditorStyles.foldout);
        window.foldoutBoldStyle.fontStyle = FontStyle.Bold;

        // Grey label style
        window.greyLabelStyle = new GUIStyle(EditorStyles.label);
        window.greyLabelStyle.normal.textColor = Color.grey;
        window.greyLabelStyle.hover.textColor = Color.grey;
    }

    void OnEnable()
    {
        // Selections
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        RefreshComponentTypes();
        RefreshSelectedComponentTypes();
    }

    // Called when the instance is destroyed or before a domain reload.
    void OnDisable()
    {
        // Selections
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
    }

    void OnGUI()
    {
        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
        EditorGUILayout.Space(1f);
        DoImportGUI();
        EditorGUILayout.Space(1f);
        DoCombinerGUI();
        EditorGUILayout.Space(1f);
        DoSelectionGUI();
        EditorGUILayout.Space(1f);
        DoGroupGUI();
        EditorGUILayout.Space(1f);
        DoStripGUI();
        EditorGUILayout.Space(1f);
        DoMiscGUI();


        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Made with Love by Balrond & probablykory", greyLabelStyle);
        GUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();

        selectionChanged = false;
    }

    private void EngageRenameMode()
    {
        if (EditorApplication.timeSinceStartup >= renameTime)
        {
            EditorApplication.update -= EngageRenameMode;
            EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
            EditorApplication.ExecuteMenuItem("Edit/Rename");
        }
    }


    #region Import Tool
    #region Utils

    private void ClearParse()
    {
        blueprintPrefabCount = 0;
        blueprintsList.Clear();
        cachedPrefabs.Clear();
        structuresList.Clear();
    }

    private GameObject FindPrefab(BlueprintPrefab blueprintPrefab, int index)
    {
        if (cachedPrefabs.Count > 0)
        {
            GameObject foundOne = cachedPrefabs.Find((x) => x.name == blueprintPrefab.prefabName);
            if (foundOne != null)
            {
                return foundOne;
            }
        }

        GameObject prefab = (GameObject)AssetDatabase.LoadAssetAtPath("Assets/PrefabInstance/" + blueprintPrefab.prefabName + ".prefab", typeof(GameObject));
        if (prefab != null)
        {
            cachedPrefabs.Add(prefab);
            return prefab;

        }
        Debug.Log("I cant find: " + blueprintPrefab.prefabName + " INDEX: " + index);
        return null;
    }

    private void GenerateStructureList()
    {
        var prefab = default(BlueprintPrefab);

        // Debug.Log("Detected PlanBuild");
        foreach (string str in blueprintsList)
        {
            if (!string.IsNullOrEmpty(str) && str.IndexOf('#') == -1)
            {

                prefab = default(BlueprintPrefab);
                string[] array = str.Split(';');
                if (array.Length >= 4)
                {
                    prefab = new BlueprintPrefab();
                    prefab.prefabName = array[0];
                    prefab.type = array[1] != null ? array[1] : "NONE";
                    prefab.posX = float.Parse(array[2], System.Globalization.CultureInfo.InvariantCulture);
                    prefab.posY = float.Parse(array[3], System.Globalization.CultureInfo.InvariantCulture);
                }
                if (array.Length >= 9)
                {
                    prefab.posZ = array[4] != null ? float.Parse(array[4], System.Globalization.CultureInfo.InvariantCulture) : 0;
                    prefab.rotX = float.Parse(array[5], System.Globalization.CultureInfo.InvariantCulture);
                    prefab.rotY = float.Parse(array[6], System.Globalization.CultureInfo.InvariantCulture);
                    prefab.rotZ = float.Parse(array[7], System.Globalization.CultureInfo.InvariantCulture);
                    prefab.rotW = array[8] != null ? float.Parse(array[8], System.Globalization.CultureInfo.InvariantCulture) : 0;
                }
                if (!BlueprintPrefab.IsDefault(prefab))
                {
                    structuresList.Add(prefab);
                }
            }
        }
        blueprintPrefabCount = structuresList.Count;
        // Debug.Log("Amount of objects: " + planBuildPrefabs.Count);
    }

    private void ImportObjects()
    {
        int counter = 0;

        if (importTarget == null)
        {
            Debug.LogError("No import target set.");
            return;
        }
        if (blueprintAsset == null)
        {
            Debug.LogError("No blueprint set.");
            return;
        }
        foreach (BlueprintPrefab blueprintPrefab in structuresList)
        {
            if (blueprintPrefab.prefabName == null)
            {
                continue;
            }

            GameObject prefab = FindPrefab(blueprintPrefab, counter);
            if (prefab != null)
            {
                Quaternion quat = new Quaternion(blueprintPrefab.rotX, blueprintPrefab.rotY, blueprintPrefab.rotZ, blueprintPrefab.rotW);
                // TODO - detect if prefab is part of an actual prefab, and use PrefabUtility
                Instantiate(prefab, new Vector3(blueprintPrefab.posX, blueprintPrefab.posY, blueprintPrefab.posZ), quat, importTarget.transform);

                // if (strip)
                // {
                //     GameObject build = Instantiate(prefab, new Vector3(prefab.posX, prefab.posY, prefab.posZ), quat, importTarget.transform);
                //     stripComponent(build);
                // }
                // else
                // {
                //    Instantiate...
                // }
            }
            counter++;
        }
        // Debug.Log("Line Done: " + counter);
    }

    #endregion

    #region State

    private bool showImports = true;
    private TextAsset blueprintAsset;
    private int blueprintPrefabCount = 0;
    List<string> blueprintsList = new List<string>();
    List<GameObject> cachedPrefabs = new List<GameObject>();
    List<BlueprintPrefab> structuresList = new List<BlueprintPrefab>();
    private GameObject importTarget;

    #endregion

    #region GUI

    private void DoImportGUI()
    {
        // Toolbar
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        showImports = EditorGUILayout.Foldout(showImports, "Import", true, foldoutBoldStyle);
        GUILayout.EndHorizontal();

        if (showImports)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();

            // Blueprint file asset
            EditorGUILayout.BeginHorizontal();
            string blueprintLabel = "Blueprint";
            if (blueprintAsset != null)
            {
                blueprintLabel += (blueprintPrefabCount > 0 ? $" ({blueprintPrefabCount} objs)" : "");
            }
            EditorGUILayout.LabelField(blueprintLabel, GUILayout.MaxWidth(125f));
            var asset = (TextAsset)EditorGUILayout.ObjectField(blueprintAsset, typeof(TextAsset), true);
            EditorGUILayout.EndHorizontal();

            if (!object.ReferenceEquals(blueprintAsset, asset))
            {
                ClearParse();
                blueprintAsset = asset;
                if (blueprintAsset != null && blueprintAsset.text.Length > 0)
                {
                    blueprintsList = blueprintAsset.ToList();
                }
                GenerateStructureList();
            }

            // Import Target
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Import Target", GUILayout.MaxWidth(125f));
            importTarget = (GameObject)EditorGUILayout.ObjectField(importTarget, typeof(GameObject), true);
            EditorGUILayout.EndHorizontal();
            if (blueprintAsset != null && blueprintPrefabCount <= 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No prefabs found within this text asset.", MessageType.Warning, true);
            }

            // Buttons (Import)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(2f);
            if (GUILayout.Button("Import", EditorStyles.miniButtonLeft))
            {
                ImportObjects();
            }
            // Buttons (Reset)
            if (GUILayout.Button("Reset", EditorStyles.miniButtonRight))
            {
                ClearParse();
                importTarget = (GameObject)EditorGUILayout.ObjectField(null, typeof(GameObject), true);
                blueprintAsset = (TextAsset)EditorGUILayout.ObjectField(null, typeof(TextAsset), true);
                Repaint();
            }
            EditorGUILayout.Space(2f);
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.Space(18);
            EditorGUILayout.EndVertical();
        }
    }

    #endregion
    #endregion

    #region Combiner Tool

    #region Utils

    private void GetAllMeshFilter()
    {
        List<MeshFilter> filters = new List<MeshFilter>();
        Renderer[] renderers = combineSource.GetComponentsInChildren<Renderer>(addHiddenObjects);
        foreach (Renderer render in renderers)
        {
            if (render.enabled)
            {
                MeshFilter singleMesh = render.GetComponent<MeshFilter>();
                if (singleMesh != null)
                {
                    filters.Add(singleMesh);
                }
            }
        }

        meshFilters = filters.ToArray();
    }

    public void EnableRenderers(bool e)
    {
        for (Int32 i = 0; i < combinedGameOjects.Length && !(combinedGameOjects[i] == null); i++)
        {
            Renderer component = combinedGameOjects[i].GetComponent<Renderer>();
            if (component != null)
            {
                component.enabled = e;
            }
        }
    }

    private void CreateNewGameObject()
    {
        combineTarget = new GameObject();
        combineTarget.name = "_Combined Mesh [" + base.name + "]";
        combineTarget.gameObject.AddComponent<MeshFilter>();
        combineTarget.gameObject.AddComponent<MeshRenderer>();
    }


    // private void CombineMeshesBalrond()
    // {
    //     GameObject gameObject = new GameObject();
    //     gameObject.name = "_Combined Mesh [" + base.name + "]";
    //     gameObject.gameObject.AddComponent<MeshFilter>();
    //     gameObject.gameObject.AddComponent<MeshRenderer>();
    //     MeshFilter[] array = meshFilters;
    //     combinedGameOjects = new GameObject[array.Length];
    // }

    public void CombineMeshes()
    {
        MeshFilter[] array = meshFilters;
        ArrayList arrayList = new ArrayList();
        ArrayList arrayList2 = new ArrayList();
        combinedGameOjects = new GameObject[array.Length];

        for (Int32 i = 0; i < array.Length; i++)
        {
            combinedGameOjects[i] = array[i].gameObject;
            MeshRenderer component = array[i].GetComponent<MeshRenderer>();
            array[i].gameObject.GetComponent<Renderer>().enabled = false;

            if (array[i].sharedMesh == null)
            {
                break;
            }

            for (Int32 j = 0; j < array[i].sharedMesh.subMeshCount; j++)
            {
                if (component == null)
                {
                    break;
                }
                if (j < component.sharedMaterials.Length && j < array[i].sharedMesh.subMeshCount)
                {
                    Int32 num = Contains(arrayList, component.sharedMaterials[j]);
                    if (num == -1)
                    {
                        arrayList.Add(component.sharedMaterials[j]);
                        num = arrayList.Count - 1;
                    }
                    arrayList2.Add(new ArrayList());
                    CombineInstance combineInstance = default(CombineInstance);
                    combineInstance.transform = component.transform.localToWorldMatrix;
                    combineInstance.mesh = array[i].sharedMesh;
                    combineInstance.subMeshIndex = j;
                    (arrayList2[num] as ArrayList).Add(combineInstance);
                }
            }

            DestroyImmediate(array[i].GetComponent<MeshRenderer>());
            DestroyImmediate(array[i]);
        }

        Mesh[] array2 = new Mesh[arrayList.Count];
        CombineInstance[] array3 = new CombineInstance[arrayList.Count];

        for (Int32 k = 0; k < arrayList.Count; k++)
        {
            CombineInstance[] combine = (arrayList2[k] as ArrayList).ToArray(typeof(CombineInstance)) as CombineInstance[];
            array2[k] = new Mesh();
            array2[k].indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            array2[k].CombineMeshes(combine, true, true);
            array3[k] = default(CombineInstance);
            array3[k].mesh = array2[k];
            array3[k].subMeshIndex = 0;
            array3[k].mesh.name = "MyNewCombinedMesh";
        }
        Mesh mesh2 = (combineTarget.GetComponent<MeshFilter>().sharedMesh = new Mesh());
        mesh2.name = "MyNewCombinedMesh";
        Mesh mesh3 = mesh2;
        mesh3.Clear();
        mesh3.CombineMeshes(array3, false, false);
        combineTarget.GetComponent<MeshFilter>().sharedMesh = mesh3;
        Mesh[] array4 = array2;
        foreach (Mesh obj in array4)
        {
            obj.Clear();
            DestroyImmediate(obj);
        }
        MeshRenderer meshRenderer = combineTarget.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = combineTarget.AddComponent<MeshRenderer>();
        }
        Material[] materials = arrayList.ToArray(typeof(Material)) as Material[];
        meshRenderer.materials = materials;
        combined = combineTarget.gameObject;
        EnableRenderers(false);

        combineTarget.GetComponent<MeshFilter>().sharedMesh.RecalculateBounds();
        vCount = (Int32)combineTarget.GetComponent<MeshFilter>().sharedMesh.vertexCount;
        // if (vCount > 65536)
        // {
        //	 Debug.LogWarning("Vertex Count: " + vCount + "- Vertex Count too high, please divide mesh combine into more groups. Max 65536 for each mesh");
        //	 _canGenerateLightmapUV = false;
        // }
        // else
        // {
        _canGenerateLightmapUV = true;
        // }
        if (setStatic)
        {
            combined.isStatic = true;
        }

        Transform[] transforms = combineSource.GetComponentsInChildren<Transform>(true);
        foreach (Transform tran in transforms)
        {
            if (tran != null && tran.gameObject.activeSelf == false)
            {
                DestroyImmediate(tran.gameObject);
            }
            if (tran != null && tran.GetComponent<Collider>() == null && tran.childCount == 0)
            {
                DestroyImmediate(tran.gameObject);
            }
        }
        combined.transform.parent = combineSource.transform;
    }

    public Int32 Contains(ArrayList l, Material n)
    {
        for (Int32 i = 0; i < l.Count; i++)
        {
            if (l[i] as Material == n)
            {
                return i;
            }
        }
        return -1;
    }

    #endregion

    #region State

    private bool showCombiner = true;
    GameObject combineSource;
    GameObject combineTarget = null;
    MeshFilter[] meshFilters = null;
    // string meshName = "Combined_Meshes";
    bool addHiddenObjects = false;
    public GameObject[] combinedGameOjects;
    public GameObject combined;

    public bool _canGenerateLightmapUV;
    public Int32 vCount;
    public bool generateLightmapUV;
    public float lightmapScale = 1f;
    public GameObject copyTarget;
    public bool destroyOldColliders;
    public bool keepStructure = true;
    public Mesh autoOverwrite;
    public bool setStatic = true;

    #endregion

    #region GUI

    private void DoCombinerGUI()
    {

        // Toolbar
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        showCombiner = EditorGUILayout.Foldout(showCombiner, "Mesh Combine", true, foldoutBoldStyle);
        GUILayout.EndHorizontal();

        if (showCombiner)
        {
            GUILayout.BeginVertical();
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField("Set a GameObject to Scan and combine");
            combineSource = (GameObject)EditorGUILayout.ObjectField(combineSource, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(2f);
            if (GUILayout.Button("Combine", EditorStyles.miniButtonLeft))
            {
                GetAllMeshFilter();
                CreateNewGameObject();
                CombineMeshes();
                Repaint();
            }
            if (GUILayout.Button("Reset", EditorStyles.miniButtonRight))
            {
                combineSource = (GameObject)EditorGUILayout.ObjectField(null, typeof(GameObject), true);
                Repaint();
            }
            EditorGUILayout.Space(2f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(18);
            GUILayout.EndVertical();
        }

    }

    #endregion

    #endregion

    #region Selections Tool

    #region Utils

    private void OnSelectionChanged()
    {
        selectionChanged = true;
        string t = "";

        selectionCount = Selection.objects.Length;
        if (selectionCount > 1)
        {
            t = $"{selectionCount} active selections" + System.Environment.NewLine;
        }
        else if (selectionCount > 0)
        {
            t = $"{selectionCount} active selection" + System.Environment.NewLine;
        }
        else
        {
            t = "Selections";
        }

        text = t.Trim();
        Repaint();
        // Debug.Log("OnSelectionChanged triggered.");
    }

    private void OnHierarchyChanged()
    {
        RefreshComponentTypes();
    }

    private void OnSceneChanged(Scene s1, Scene s2)
    {
        RefreshComponentTypes();
    }

    private void RefreshComponentTypes()
    {
        if (targetMode == 0)
        {
            RefreshComponentTypes<MonoBehaviour>();
        }
        else
        {
            RefreshComponentTypes<Component>();
        }
    }

    private Type GetTypeTargetMode()
    {
        if (targetMode == 0)
        {
            return typeof(MonoBehaviour);
        }
        return typeof(Component);
    }

    private void RefreshComponentTypes<T>()
    {
        RefreshComponentTypes(typeof(T));
    }

    private void RefreshComponentTypes(Type type)
    {
        componentTypes = GetAllComponentTypes(type).Distinct().ToList();
        componentTypeStrings = componentTypes.Select(t => t.ToString()).OrderBy(s => s).ToArray();

        // used in Strip tool
        if (componentTypeFlags.Count == componentTypes.Count)
        {
            componentTypeFlags = componentTypeStrings
                .Select((v) => new { Key = v, Value = componentTypeFlags[v] })
                .ToDictionary(o => o.Key, o => o.Value);
        }
        else
        {
            componentTypeFlags = componentTypeStrings
                .Select((v) => new { Key = v, Value = false })
                .ToDictionary(o => o.Key, o => o.Value);
        }
    }

    private IEnumerable<Type> GetAllComponentTypes(Type type)
    {
        return SceneManager
            .GetActiveScene()
            .GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren(type, selectInactive))
            .Select(c => c.GetType());
    }


    private void SelectParentComponents(string componentType)
    {
        List<GameObject> results = new List<GameObject>();
        Type selectedType = componentTypes.FirstOrDefault(t => t.ToString() == componentType);

        foreach (var child in Selection.gameObjects)
        {
            Component comp = null;
            GameObject target = child;
            while (target != null)
            {
                target = target.transform?.parent?.gameObject;
                comp = target?.GetComponent(selectedType);
                if (comp != null)
                {
                    break;
                }
            }

            if (target != null)
            {
                results.Add(target);
            }
        }

        if (results.Count > 0)
        {
            Selection.objects = results.ToArray();
        }
        else
        {
            Debug.LogError("No game objects found");
        }

        // Debug.Log($"Selected type {selectedType}, result count {results.Count}");
    }

    private void SelectAllComponents(string componentType)
    {
        // DateTime start = DateTime.Now;

        List<GameObject> results = new List<GameObject>();
        Type selectedType = componentTypes.FirstOrDefault(t => t.ToString() == componentType);

        // Debug.Log($"Selected type {selectedType}");

        results = SceneManager
            .GetActiveScene()
            .GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren(selectedType, selectInactive))
            .Select(c => c.gameObject).ToList();

        // Debug.Log($"SceneManager RootObjects GetComponentsInChildren ran. {DateTime.Now.Subtract(start).Milliseconds}ms");

        if (!selectInactive)
        {
            results = results.Where(go => go.activeInHierarchy == true).ToList();
        }

        // Debug.Log($"Processed inactive. {DateTime.Now.Subtract(start).Milliseconds}ms");

        if (results.Count > 1000)
        {
            if (EditorUtility.DisplayDialog(
                "Confirm Selection",
                $"Are you sure want to select {results.Count} objects?",
                "Yes", // OK button
                "No" // Cancel button
            ))
            {
                Selection.objects = results.ToArray();
            }
            else
            {
                Debug.LogError("Selection cancelled.");
            }

        }
        else if (results.Count > 0)
        {
            Selection.objects = results.ToArray();
        }
        else
        {
            Debug.LogError("No game objects found");
        }
        // Debug.Log($"Selection made. {DateTime.Now.Subtract(start).Milliseconds}ms");
    }

    #endregion

    #region State
    private bool selectionChanged = false;
    private bool showSelections = true;
    private bool selectInactive = true;
    private string text = "Selections";
    private int selectionCount = 0;
    private int targetMode = 0;
    private List<Type> componentTypes;
    private string[] componentTypeStrings;
    private int selectedComponentType = 0;
    private string selectActionButtonText = "";
    #endregion

    #region GUI
    private void DoSelectionGUI()
    {
        // Toolbar
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        showSelections = EditorGUILayout.Foldout(showSelections, text, true, foldoutBoldStyle);
        GUILayout.EndHorizontal();

        if (showSelections)
        {

            // Selection mode
            GUILayout.BeginVertical();
            EditorGUILayout.Space();
            string[] labels = new string[2] { "MonoBehaviours", "Components" };
            targetMode = GUILayout.Toolbar(targetMode, labels, "LargeButton");
            if (GUI.changed)
            {
                RefreshComponentTypes();
                if (selectedComponentType >= componentTypeStrings.Length)
                {
                    selectedComponentType = 0;
                }
            }

            selectInactive = EditorGUILayout.ToggleLeft("Select hidden", selectInactive);

            // Component Type to select
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            selectedComponentType = EditorGUILayout.Popup(selectedComponentType, componentTypeStrings);
            GUILayout.Space(4);

            // Select button
            if (selectionCount > 0)
                selectActionButtonText = "Select Parents...";
            else
                selectActionButtonText = "Select All...";
            if (GUILayout.Button(selectActionButtonText, GUILayout.Width(135f)))
            {
                string name = componentTypeStrings[selectedComponentType];
                if (selectionCount > 0)
                {
                    SelectParentComponents(name);
                }
                else
                {
                    SelectAllComponents(name);
                }
            }
            GUILayout.Space(2);
            GUILayout.EndHorizontal();

            // Deselect button
            if (selectionCount > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Deselect All", GUILayout.Width(135f)))
                {
                    Selection.activeGameObject = null;
                }
                GUILayout.Space(2);
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(18);
            GUILayout.EndVertical();
        }
    }
    #endregion
    #endregion

    #region Strip Tool

    #region Utils

    private int stripComponent(GameObject prefab, Type type)
    {
        int counter = 0;
        // Debug.Log("Found: " + (type != null ? type.Name : "EMPTY"));
        Component[] components = prefab.GetComponentsInChildren(type, true);
        foreach (Component component in components)
        {
            DestroyImmediate(component);
            counter++;
        }
        return counter;
    }

    private void stripSelections()
    {
        int counter = 0;
        foreach (var key in componentTypeFlags.Keys)
        {
            Type selectedType = componentTypes.FirstOrDefault(t => t.ToString() == key);
            if (componentTypeFlags[key])
            {
                counter = 0;
                foreach (var go in Selection.gameObjects)
                {
                    counter += stripComponent(go, selectedType);
                }
                Debug.Log($"Striped {counter} components of type {selectedType}.");
            }
        }
    }

    #endregion

    #region State

    private bool showStrip = true;
    private Dictionary<string, bool> componentTypeFlags = new Dictionary<string, bool>();

    #endregion

    #region GUI

    private void DoStripGUI()
    {

        // Toolbar
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        showStrip = EditorGUILayout.Foldout(showStrip, "Strip", true, foldoutBoldStyle);
        GUILayout.EndHorizontal();

        if (showStrip)
        {
            GUILayout.BeginVertical();

            // type toggles
            var updatedFlags = new Dictionary<string, bool>(componentTypeFlags);
            foreach (var key in componentTypeFlags.Keys)
            {
                updatedFlags[key] = EditorGUILayout.ToggleLeft(key, componentTypeFlags[key]);
            }
            componentTypeFlags = updatedFlags;

            // Strip button

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = selectionCount > 0;
            if (GUILayout.Button("Strip", GUILayout.Width(135f)))
            {
                stripSelections();
                componentTypeFlags.Clear();
                RefreshComponentTypes();
            }
            GUI.enabled = true;
            GUILayout.Space(2);
            GUILayout.EndHorizontal();


            EditorGUILayout.Space(18);
            GUILayout.EndVertical();
        }
    }

    #endregion

    #endregion

    #region Group Tool

    #region Utils

    private void GroupObjects()
    {

        if (Selection.gameObjects?.Length > 0)
        {
 
            var firstObject = Selection.gameObjects[0];
            var index = firstObject.transform.GetSiblingIndex();
            var parent = firstObject.transform.parent;
 
            var target = new GameObject();
            target.name = "Group - " + firstObject.name;
 
            for (int i = 0; i < Selection.gameObjects.Length; i++)
            {
                // Selection.gameObjects[i].transform.parent = gameObject.transform;
                Undo.SetTransformParent(Selection.gameObjects[i].transform, target.transform, false, "Group objects");

                if (transferComponents)
                {
                    foreach (var key in groupComponentTypeFlags.Keys)
                    {
                        var go = Selection.gameObjects[i];
                        Type selectedType = componentTypes.FirstOrDefault(t => t.ToString() == key);
                        if (groupComponentTypeFlags[key] && go.HasComponent(selectedType))
                        {
                            Component comp = go.GetComponent(selectedType);
                            if (target.HasComponent(selectedType))
                            {
                                // Destroy if parent already has component
                                DestroyImmediate(comp);
                            }
                            else
                            {
                                // Move if parent doesn't have component
                                comp.MoveToGameObject(target);
                            }
                        }
                    }
                }
            }
 
            if (parent != null)
            {
                // gameObject.transform.parent = parent.transform;
                Undo.SetTransformParent(target.transform, parent.transform, false, "Group objects");
            }
 
            Selection.objects = new [] { target };
            renameTime = EditorApplication.timeSinceStartup + 0.2d;
            EditorApplication.update += EngageRenameMode;
        }
    }

    private void RefreshSelectedComponentTypes()
    {
        var type = GetTypeTargetMode();
        var selectedCompTypes = Selection.gameObjects
            .SelectMany(go => go.GetComponentsInChildren(type, selectInactive))
            .Select(c => c.GetType())
            .Distinct().ToList();

        // Debug.Log($"selectedCompTypes {selectedCompTypes.Count}");

        groupComponentTypeOptions = selectedCompTypes
            .Select(t => t.ToString()).OrderBy(s => s).ToArray();

        // Debug.Log($"groupComponentTypeOptions {groupComponentTypeOptions.Count()}");

        if (groupComponentTypeFlags.Count == selectedCompTypes.Count)
        {
            groupComponentTypeFlags = groupComponentTypeOptions
                .Select((v) => new { Key = v, Value = groupComponentTypeFlags[v] })
                .ToDictionary(o => o.Key, o => o.Value);
        }
        else
        {
            groupComponentTypeFlags = groupComponentTypeOptions
                .Select((v) => new { Key = v, Value = false })
                .ToDictionary(o => o.Key, o => o.Value);
        }
        // Debug.Log($"groupComponentTypeFlags {groupComponentTypeFlags.Count}");
    }

    #endregion

    #region State

    bool transferComponents = false;
    private Dictionary<string, bool> groupComponentTypeFlags = new Dictionary<string, bool>();
    private string[] groupComponentTypeOptions;
    bool showGroup = true;
    
    #endregion

    #region GUI

    private void DoGroupGUI()
    {

        // Toolbar
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        showGroup = EditorGUILayout.Foldout(showGroup, "Group", true, foldoutBoldStyle);
        GUILayout.EndHorizontal();

        if (showGroup)
        {
            GUILayout.BeginVertical();

            if (GUI.changed || selectionChanged)
            {
                RefreshSelectedComponentTypes();
            }

            transferComponents = EditorGUILayout.ToggleLeft("Transfer Components to new parent", transferComponents);
            if (transferComponents)
            {
                // type toggles
                var updatedFlags = new Dictionary<string, bool>(groupComponentTypeFlags);
                foreach (var key in groupComponentTypeFlags.Keys)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    updatedFlags[key] = EditorGUILayout.ToggleLeft(key, groupComponentTypeFlags[key]);
                    GUILayout.EndHorizontal();
                }
                groupComponentTypeFlags = updatedFlags;
            }

            // Group button
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = selectionCount > 0;
            if (GUILayout.Button("Group", GUILayout.Width(135f)))
            {
                GroupObjects();
            }
            GUI.enabled = true;
            GUILayout.Space(2);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }
    }

    #endregion

    #endregion

    #region Misc Tools

    private void SetImmortal(WearNTear wearNTear)
    {
        wearNTear.m_damages.m_slash = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_blunt = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_pierce = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_fire = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_spirit = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_lightning = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_poison = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_frost = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_pickaxe = HitData.DamageModifier.Immune;
        wearNTear.m_damages.m_chop = HitData.DamageModifier.Immune;
        wearNTear.m_noRoofWear = false;
        wearNTear.m_noSupportWear = false;

        wearNTear.m_destroyedEffect = null;
    }

    private void SetSelectionsToImmortal()
    {
        Type wntType = typeof(WearNTear);

        foreach (var go in Selection.gameObjects)
        {
            var wnt = go.GetComponent(wntType) as WearNTear;
            if (wnt == null)
            {
                wnt = go.AddComponent(wntType) as WearNTear;
            }
            SetImmortal(wnt);
        }
    }

    private bool showMisc = false;
    private void DoMiscGUI()
    {

        // Toolbar
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        showMisc = EditorGUILayout.Foldout(showMisc, "Misc", true, foldoutBoldStyle);
        GUILayout.EndHorizontal();

        if (showMisc)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(6);
            GUI.enabled = selectionCount > 0;
            if (GUILayout.Button("Set WearNTear to immortal"))
            {
                SetSelectionsToImmortal();
            }
            GUI.enabled = true;

            GUILayout.Space(4);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(6);
            EditorGUILayout.HelpBox("  Creates a WearNTear if one isn't present.", MessageType.Info, true);
            GUILayout.Space(4);
            GUILayout.EndHorizontal();

        }
    }

    #endregion
}
