using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CaptureReplay : MonoBehaviour
{
    public bool capture = false;
    public bool replay = false;
    public bool iterate = false;

    private Dictionary<uint, GameObject> activeObjs;
    private bool capturing = false;
    private bool replaying = false;
    private bool iterating = false;
    private int startFrame = 0;

    private string traceName = null;
    private StreamWriter writer = null;
    private StreamReader reader = null;

    private Camera camera;
    private double totalGray = 0;
    private uint frameCount = 0;
    private uint currentLight = 0;
    private Light[] lights;
    private List<KeyValuePair<double, Light>> lightAvgGray;
    private bool[] prevLightEnables;

    private Texture2D stagingBuffer;
    private RenderTexture framebuffer;

    struct ActionReplayer {
        int frameIdx;
        uint objId;
        string type;
        float x;
        float y;
        float z;
        float w;

        public bool IsValid() { return type != null; }
        void Clear() { type = null; }

        public int GetFrameIdx() { return frameIdx; }

        public bool IsEnd() { return type == "end"; }

        public void ReadLine(StreamReader reader)
        {
            if (!IsValid() && !reader.EndOfStream)
            {
                string[] line = reader.ReadLine().Split(',');

                if (!Int32.TryParse(line[0], out frameIdx))
                {
                    Debug.LogError("Error reading trace file: Unable to read frameIdx field!");
                }
                if (!UInt32.TryParse(line[1], out objId))
                {
                    Debug.LogError("Error reading trace file: Unable to read objId field!");
                }
                if ((type = line[2]).Length == 0)
                {
                    Debug.LogError("Error reading trace file: Unable to read type field!");
                }
                if (!IsEnd())
                {
                    if (!Single.TryParse(line[3], out x))
                    {
                        Debug.LogError("Error reading trace file: Unable to read x field!");
                    }
                    if (!Single.TryParse(line[4], out y))
                    {
                        Debug.LogError("Error reading trace file: Unable to read y field!");
                    }
                    if (!Single.TryParse(line[5], out z))
                    {
                        Debug.LogError("Error reading trace file: Unable to read z field!");
                    }
                    if (type == "rot")
                    {
                        if (!Single.TryParse(line[6], out w))
                        {
                            Debug.LogError("Error reading trace file: Unable to read w field!");
                        }
                    }
                }
            }
        }

        public void ApplyLine(Dictionary<uint, GameObject> activeObjs)
        {
            if (IsValid() && !IsEnd())
            {
                if (type == "pos")
                {
                    activeObjs[objId].transform.position = new Vector3(x, y, z);
                }
                else if (type == "rot")
                {
                    activeObjs[objId].transform.rotation = new Quaternion(x, y, z, w);
                }
                else
                {
                    Debug.LogError("Error reading trace file: Encountered invalid type!");
                }

                Clear();
            }
        }
    };
    ActionReplayer replayer = new ActionReplayer();

    private void Reset()
    {
        foreach (GameObject obj in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            Identifier id = obj.GetComponent<Identifier>();

            if (id == null)
            {
                obj.AddComponent<Identifier>();
            }
            else
            {
                id.Reset();
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        traceName = Application.dataPath + "/trace_" + SceneManager.GetActiveScene().name + ".csv";
        activeObjs = new Dictionary<uint, GameObject>();

        camera = GetComponent<Camera>();
        stagingBuffer = new Texture2D(camera.scaledPixelWidth, camera.scaledPixelHeight, TextureFormat.RGB24, false);
        framebuffer = new RenderTexture(camera.scaledPixelWidth, camera.scaledPixelHeight, 24);

        foreach (GameObject obj in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            Identifier id = obj.GetComponent<Identifier>();

            if (obj.activeInHierarchy && !obj.isStatic && id != null)
            {
                if (id.value != 0)
                {
                    activeObjs.Add(id.value, obj);
                }
                else
                {
                    Debug.LogError("Object " + obj.name + " has an invalid id of 0!");
                }
            }
        }
    }

    private void BeginCapture()
    {
        writer = new StreamWriter(traceName);
        startFrame = Time.frameCount;
        capturing = true;
    }

    private void EndCapture()
    {
        int frameIdx = Time.frameCount - startFrame;
        writer.WriteLine(frameIdx + "," + UInt32.MaxValue + ",end");

        capturing = false;
        writer.Close();
        writer = null;
    }

    private void CaptureUpdate()
    {
        if (capture && !capturing)
        {
            BeginCapture();
        }
        else if (!capture && capturing)
        {
            EndCapture();
        }

        if (capturing)
        {
            foreach (KeyValuePair<uint, GameObject> obj in activeObjs)
            {
                Identifier id = obj.Value.GetComponent<Identifier>();

                Vector3 pos;
                bool posUpdated = id.GetPos(out pos);

                Quaternion rot;
                bool rotUpdated = id.GetRot(out rot);

                int frameIdx = Time.frameCount - startFrame;

                if (posUpdated)
                {
                    writer.WriteLine(frameIdx + "," + id.value + ",pos," + pos.x + "," + pos.y + "," + pos.z);
                }

                if (rotUpdated)
                {
                    writer.WriteLine(frameIdx + "," + id.value + ",rot," + rot.x + "," + rot.y + "," + rot.z + "," + rot.w);
                }
            }
        }
    }

    private void BeginReplay()
    {
        reader = new StreamReader(traceName);
        startFrame = Time.frameCount;
        replaying = true;
    }

    private void EndReplay()
    {
        replaying = false;
        replay = false;
        replayer = new ActionReplayer();
        reader.Close();
        reader = null;
    }

    private void ReplayUpdate()
    {
        if (replay && !replaying)
        {
            BeginReplay();
        }
        else if (!replay && replaying)
        {
            EndReplay();
        }

        if (replaying)
        {
            int frameIdx = Time.frameCount - startFrame;

            while (true)
            {
                replayer.ReadLine(reader);

                if (!replayer.IsValid())
                {
                    EndReplay();
                    break;
                }
                else if (frameIdx == replayer.GetFrameIdx())
                {
                    if (replayer.IsEnd())
                    {
                        EndReplay();
                        break;
                    }
                    else
                    {
                        replayer.ApplyLine(activeObjs);
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }

    string GetGameObjectPath(GameObject obj)
    {
        string path = "/" + obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = "/" + obj.name + path;
        }
        return path;
    }

    void CopyFramebuffer()
    {
        RenderTexture prevTargetTexture = camera.targetTexture;
        camera.targetTexture = framebuffer;
        camera.Render();
        RenderTexture prevActiveRT = RenderTexture.active;
        RenderTexture.active = framebuffer;
        stagingBuffer.ReadPixels(new Rect(0, 0, stagingBuffer.width, stagingBuffer.height), 0, 0);
        stagingBuffer.Apply();
        camera.targetTexture = prevTargetTexture;
        RenderTexture.active = prevActiveRT;
    }

    double ConvertToGrayscale()
    {
        double sum = 0;
        Color32[] pixels = stagingBuffer.GetPixels32();
        for (int x = 0; x < stagingBuffer.width; x++)
        {
            for (int y = 0; y < stagingBuffer.height; y++)
            {
                Color32 pixel = pixels[x + y * stagingBuffer.width];
                int p = ((256 * 256 + pixel.r) * 256 + pixel.b) * 256 + pixel.g;
                int b = p % 256;
                p = Mathf.FloorToInt(p / 256);
                int g = p % 256;
                p = Mathf.FloorToInt(p / 256);
                int r = p % 256;
                float l = (0.2126f * r / 255f) + 0.7152f * (g / 255f) + 0.0722f * (b / 255f);
                Color c = new Color(l, l, l, 1);
                sum += l;
                stagingBuffer.SetPixel(x, y, c);
            }
        }
        stagingBuffer.Apply(false);
        //var bytes = stagingBuffer.EncodeToPNG();
        //System.IO.File.WriteAllBytes(Application.dataPath + "ImageSaveTest.png", bytes);
        return sum / (stagingBuffer.width * stagingBuffer.height);
    }

    void OutputResultsToFile()
    {
        using (StreamWriter writer = new StreamWriter(Application.dataPath + "/lightRanking_" + SceneManager.GetActiveScene().name + ".csv"))
        {
            List<KeyValuePair<double, Light>> sortedAvgGray = lightAvgGray.OrderByDescending(o => o.Key).ToList();

            for (int i = 0; i < sortedAvgGray.Count; ++i)
            {
                writer.WriteLine(i.ToString() + "," + GetGameObjectPath(sortedAvgGray[i].Value.gameObject) + "," + sortedAvgGray[i].Key.ToString());
            }
        }
    }

    void BeginIterate()
    {
        totalGray = 0;
        frameCount = 0;
        currentLight = 0;
        lights = FindObjectsOfType(typeof(Light)) as Light[];
        lightAvgGray = new List<KeyValuePair<double, Light>>();

        prevLightEnables = new bool[lights.Length];
        for (uint i = 0; i < lights.Length; ++i)
        {
            prevLightEnables[i] = lights[i].enabled;
            lights[i].enabled = false;
        }

        iterating = true;
    }

    void EndIterate()
    {
        OutputResultsToFile();

        iterating = false;
        iterate = false;

        for (uint i = 0; i < lights.Length; ++i)
        {
            lights[i].enabled = prevLightEnables[i];
        }
    }

    void IterateUpdate()
    {
        if (iterate && !iterating)
        {
            BeginIterate();
        }
        else if (!iterate && iterating)
        {
            EndIterate();
        }

        if (iterating)
        {
            if (!replaying)
            {
                if (frameCount > 0)
                {
                    double avgGray = totalGray / frameCount;
                    lightAvgGray.Add(new KeyValuePair<double, Light>(avgGray, lights[currentLight]));
                    Debug.Log("Average grayscale for Light [" + currentLight.ToString() + "/" + lights.Length.ToString() + "]: " + avgGray.ToString() + " (" + GetGameObjectPath(lights[currentLight].gameObject) + ")");

                    frameCount = 0;
                    totalGray = 0;
                    lights[currentLight].enabled = false;
                    currentLight++;
                }

                if (currentLight < lights.Length)
                {
                    lights[currentLight].enabled = true;
                    replay = true;
                }
                else
                {
                    EndIterate();
                }
            }
            else
            {
                CopyFramebuffer();
                totalGray += ConvertToGrayscale();
                frameCount++;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        CaptureUpdate();
        IterateUpdate();
        ReplayUpdate();
    }
}
