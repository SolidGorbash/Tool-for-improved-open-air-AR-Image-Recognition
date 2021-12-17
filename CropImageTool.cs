using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif
public class CropImageTool : MonoBehaviour, ISerializationCallbackReceiver
{
    [Header("Image name used for main folder and each cropped detail")]
    [SerializeField] string m_ImageName;

    [Header("The 2D Texture you want to crop")]
    [SerializeField] Texture2D m_Texture2D;

    
    [Header("Crop Details")]
    
    [Tooltip("Number of columns you want to divide the image in, please provide a number that divides the image witdth in pixels without remainder")]
    [SerializeField] int m_DivisionFactorX;
    [Tooltip("Number of rows you want to divide the image in, please provide a number that divides the image height in pixels without remainder")]
    [SerializeField] int m_DivisionFactorY;

    [Header("Real Object informations")]
    [Tooltip("Width (in metres) of the object your full image rapresents")]
    [SerializeField] float m_RealObjectWidth;
    [Tooltip("Height (in metrs) of the object your full image rapresents")]
    [SerializeField] float m_RealObjectHeight;

    [Header("If your use case scenario needs to swap y & z axes, check this variable.")]
    [SerializeField] bool m_InvertYZ = true;

    [Header("Use this to set your prefab at a certain distance in front of the object (metres)")]
    [SerializeField] float m_DistanceFromImage = 0.1f;

    string m_ObjectFolderPath;

    public void OnBeforeSerialize()
    {

    }

    public void OnAfterDeserialize() { }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    /// <summary>
    /// Main Function used inside Unity Editor.
    /// </summary>
    public void CropImage()
    {
        #region Preliminary Checks
        // preliminary checks
        if (m_ImageName == "")
        {
            Debug.Log("Please insert a proper image name (without spaces)");
            return;
        }
        if (m_Texture2D == null)
        {
            Debug.Log("Please provide a 2D Texture with read and write enabled in the import settings");
            return;
        }

        if (m_DivisionFactorX <= 0)
        {
            Debug.Log("Please provide a division factor for the x axis higher than zero");
            return;
        }

        if (m_DivisionFactorY <= 0)
        {
            Debug.Log("Please provide a division factor for the y axis highter than zero");
            return;
        }

        if (m_RealObjectWidth <= 0)
        {
            Debug.Log("Please provide the correct width of real object represented in the texture");
            return;
        }

        if (m_RealObjectHeight <= 0)
        {
            Debug.Log("Please provide the correct height of real object represented in the texture");
            return;
        }
        #endregion
        // check if main crop folder exist
        if (!Directory.Exists(Application.dataPath + "/ObjectDetailsImages"))
        {
            // create folder
            var mainFolder = Directory.CreateDirectory(Application.dataPath + "/ObjectDetailsImages");
        }

        // check if the folder for the current cropped image details exists
        m_ObjectFolderPath = Application.dataPath + "/ObjectDetailsImages/" + m_ImageName;
        if (!Directory.Exists(m_ObjectFolderPath))
        {
            // create folder
            var objectDetailsFolder = Directory.CreateDirectory(Application.dataPath + "/ObjectDetailsImages/" + m_ImageName);
        }
        
        DivideWithRealMesaures();

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    /// <summary>
    /// Wraps up all operations for the division of texture, inserting real measures data in the image name.
    /// </summary>
    void DivideWithRealMesaures()
    {
        // calculate object centre position using provided real measures (metres)
        Vector2 imagePivot = new Vector2(m_RealObjectWidth / 2, m_RealObjectHeight / 2);
        // calculate each segment pixel dimensions for the cropped teture output
        int cropWidth = m_Texture2D.width / m_DivisionFactorX;
        int cropHeight = m_Texture2D.height / m_DivisionFactorY;
        // calculate each segment metre dimensions for AR positioning data
        float realCropWidth = m_RealObjectWidth / m_DivisionFactorX;
        float realCropHeight = m_RealObjectHeight / m_DivisionFactorY;
        // image total number
        int imageNumber = 0;

        for (int r = 0; r < m_DivisionFactorY; r++)
        {
            for (int c = 0; c < m_DivisionFactorX; c++)
            {
                // distance from whole image centre (metri)
                float realDistanceX = imagePivot.x - ((realCropWidth / 2f) + (realCropWidth * c));
                float realDistanceY = imagePivot.y - ((realCropHeight / 2f) + (realCropHeight * r));

                string name = m_ImageName + "Crop" + imageNumber + "X" + realDistanceX + "Y" + realDistanceY;
                // create new texture with the cropped portion using pixels
                DivideImage(cropWidth * c, cropHeight * r, cropWidth, cropHeight, name);
                // create correspongind prefab for cropped detail
                CreatePrefab(name);
                // update image counter
                imageNumber++;
            }
        }
    }

    /// <summary>
    /// Divides an image in smaller chunk with the provided parameters and saves it in the custom folder.
    /// </summary>
    /// <param name="startX">Crop Start Position for the x axis</param>
    /// <param name="startY">Crop Start Position for the y axis</param>
    /// <param name="width">Final Width for the cropped chunk</param>
    /// <param name="height">Final Height for the cropped chunk</param>
    /// <param name="name">Name for texture png output</param>
    void DivideImage(int startX, int startY, int width, int height, string name)
    {
        var pixels = m_Texture2D.GetPixels(startX, startY, width, height);
        var image = new Texture2D(width, height);
        image.SetPixels(pixels);
        image.Apply();

        var bytes = image.EncodeToPNG();
        File.WriteAllBytes(m_ObjectFolderPath + "/" + name + ".png", bytes);
    }

    /// <summary>
    /// Creates a blueprint prefab for parenting AR custom content, enabling correct centre positioning on a 2D object.
    /// </summary>
    /// <param name="name">String containing Cropped Texture Name</param>
    void CreatePrefab(string name)
    {
#if UNITY_EDITOR
        GameObject newGameObject = new GameObject();

        // extract position data from string
        Vector2 data = ExtractDataFromName(name);
        

        if (m_InvertYZ)
        {
            newGameObject.transform.position = new Vector3(data.x, m_DistanceFromImage, data.y);
        }
        else
        {
            newGameObject.transform.position = new Vector3(data.x, data.y, m_DistanceFromImage);
        }

        // save the game object as a prefab inside object folder
        PrefabUtility.SaveAsPrefabAsset(newGameObject, m_ObjectFolderPath + "/" + name + ".prefab");

        // remove game object from editor scene
        DestroyImmediate(newGameObject);
#endif
    }

    /// <summary>
    /// Extracts position data from cropped texture name.
    /// </summary>
    /// <param name="name">String containing Cropped Texture Name</param>
    /// <returns></returns>
    private Vector2 ExtractDataFromName(string name)
    {
        // find initial position for distance values
        int x = name.IndexOf("X");
        int y = name.IndexOf("Y");

        // extract distance from string and converti it to float
        float distanceX = float.Parse(name.Substring(x + 1, y - x - 1));
        float distanceY = float.Parse(name.Substring(y + 1, name.Length - y - 1));

        // create and return a Vector2 with the two dimensions
        Vector2 data = new Vector2(distanceX, distanceY);

        return data;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CropImageTool))]
class CropImageToolInspector : Editor
{
    public override void OnInspectorGUI()
    {
      
        EditorGUILayout.HelpBox("This tool divides a provided texture in smaller ones.\n" +
                                "It's important to enable read & write on the image in the import settings.\n"+
                                "Also in the import settings make sure the photo retains it's original size for better results.", MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("The script also saves in the details folder a custom prefab that can be used as a parent for your custom AR content.\n" +
                                "This way the AR content is displayed in the centre of the object.\n" +
                                "Additional position tuning may be required.", MessageType.Info);

        EditorGUILayout.Space();

        base.OnInspectorGUI();


        EditorGUILayout.Space();

        CropImageTool myScript = (CropImageTool)target;
        if (GUILayout.Button("Crop Image"))
        {
            myScript.CropImage();
        }
    }
}
#endif
