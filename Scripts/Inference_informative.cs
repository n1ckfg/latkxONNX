using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Barracuda;

public class Inference_informative : MonoBehaviour {

    public NNModel nnModel;
    public Material targetMtl;
    public string targetMtlProp = "_MainTex";
    public bool displayOutputTexture = true;
    public LightningArtist latk;
    public float skeleton_threshold = 0.5f;
    public int trace_c = 10;
    public int minPoints = 2;
    public float distanceThreshold = 1f;
    public int scaleDownFactor = 3;

    [HideInInspector] public RenderTexture inputTex;
    [HideInInspector] public RenderTexture outputTex;

    private Model model;
    private IWorker worker;
    private bool ready = true;
    private float thresholdBoolOutput;

    private void Start() {
        model = ModelLoader.Load(nnModel);
        //worker = WorkerFactory.CreateWorker(model, WorkerFactory.Device.GPU);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
        thresholdBoolOutput = Mathf.Abs(1f - Mathf.Clamp(skeleton_threshold, 0f, 1f));
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space) && ready) {
            StartCoroutine(DoInference());
        }
    }

    IEnumerator DoInference() {
        ready = false;
        Screenshot(Camera.main);

        // Do inference
        var channelCount = 3; // 1 = grayscale, 3 = rgb, 4 = rgba
        var inputX = new Tensor(inputTex, channelCount);
        Tensor outputY = worker.Execute(inputX).PeekOutput();
        inputX.Dispose(); // Barracuda objects are not GC'd
        float[] outputFloats = outputY.AsFloats();

        // View output as texture
        if (displayOutputTexture) {
            if (outputTex != null) outputTex.Release();
            outputTex = new RenderTexture(inputTex.width, inputTex.height, 0, RenderTextureFormat.ARGB32);
            outputTex.enableRandomWrite = true;
            outputTex.Create();
            SetTexFromFloats(outputFloats);
            targetMtl.SetTexture(targetMtlProp, outputTex);
        }
        
        // Convert output to polylines
        bool[] outputBools = SetBoolsFromFloats(outputFloats);
        TraceSkeleton.thinningZS(outputBools, inputTex.width, inputTex.height);
        List<List<int[]>> traceOutput = TraceSkeleton.traceSkeleton(outputBools, inputTex.width, inputTex.height, trace_c);

        float w = (float) Screen.width;
        float h = (float) Screen.height;

        List<List<Vector3>> originalStrokes = new List<List<Vector3>>();
        List<List<Vector3>> separatedStrokes = new List<List<Vector3>>();

        //Debug.Log("Found " + traceOutput.Count + " lines.");
        for (int i = 0; i < traceOutput.Count; i++) {
            List<Vector3> points = new List<Vector3>();
            //Debug.Log("Found " + traceOutput[i].Count + " points in line " + i + ".");

            for (int j = 0; j < traceOutput[i].Count; j++) {
                float x = ((float) traceOutput[i][j][0] / (float) inputTex.width) * (float) Screen.width;
                float y = ((float) traceOutput[i][j][1] / (float) inputTex.height) * (float) Screen.height;
                //Debug.Log(x + ", " + y + ", " + w + ", " + h);
                Vector2 point2D = new Vector2(x, Screen.height - y);// / w, 1f - (y / w));
                //Debug.Log(point2D.x + ", " + point2D.y);
                Vector3 point3D = FindWorldSpaceCoords(point2D);
                if (point3D != Vector3.zero) points.Add(point3D);
            }

            if (points.Count >= minPoints) {
                originalStrokes.Add(points);
            }
        }

        for (int i = 0; i < originalStrokes.Count; i++) {
            List<List<Vector3>> separatedTempList = SeparatePointsByDistance(originalStrokes[i], distanceThreshold);

            for (int j = 0; j < separatedTempList.Count; j++) {
                separatedStrokes.Add(separatedTempList[j]);
            }
        }

        for (int i=0; i<separatedStrokes.Count; i++) {
            latk.inputInstantiateStroke(Color.red, separatedStrokes[i]);
        }

        ready = true;
        yield return null;
    }

   private void OnDestroy() {
        worker?.Dispose();

        if (inputTex != null) inputTex.Release();
        if (outputTex != null) outputTex.Release();
        inputTex = null;
        outputTex = null;
    }

    private void SetTexFromFloats(float[] floatArray) {
        Debug.Log(floatArray.Length + ", " + inputTex.width + ", " + inputTex.height);
        if (floatArray.Length < inputTex.width * inputTex.height) {
            Debug.LogError("Float array size is smaller than RenderTexture size.");
            return;
        }

        Texture2D tempTexture = new Texture2D(inputTex.width, inputTex.height, TextureFormat.ARGB32, false);

        Color[] colors = new Color[inputTex.width * inputTex.height];
        for (int i = 0; i < inputTex.width * inputTex.height; i++) {
            colors[i] = new Color(floatArray[i], floatArray[i], floatArray[i], 1f);
        }
        tempTexture.SetPixels(colors);
        tempTexture.Apply();

        //Graphics.CopyTexture(tempTexture, outputTex);
        Graphics.Blit(tempTexture, outputTex, new Vector2(1.0f, -1.0f), new Vector2(0f, 0f));
                
        Destroy(tempTexture);
    }

    private bool[] SetBoolsFromFloats(float[] floatArray) {
        bool[] returns = new bool[inputTex.width * inputTex.height];
        for (int i=0; i< inputTex.width * inputTex.height; i++) {
            returns[i] = floatArray[i] < thresholdBoolOutput ? true : false;
        }
        return returns;
    }

    private void Screenshot(Camera cam) {
        if (inputTex != null) inputTex.Release();
        inputTex = new RenderTexture(Screen.width/scaleDownFactor, Screen.height/scaleDownFactor, 0, RenderTextureFormat.ARGB32);
        inputTex.enableRandomWrite = true;
        inputTex.Create();

        cam.targetTexture = inputTex;
        cam.Render();
        //RenderTexture.active = inputTex;
        cam.targetTexture = null;
        //RenderTexture.active = null;
    }

    private Vector3 FindWorldSpaceCoords(Vector2 inputPoint) {
        Ray ray = Camera.main.ScreenPointToRay(inputPoint);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit)) {
            return hit.point;
        } else {
            return Vector3.zero;
        }
    }

    List<List<Vector3>> SeparatePointsByDistance(List<Vector3> pointList, float threshold) {
        List<List<Vector3>> separated = new List<List<Vector3>>();
        List<Vector3> currentList = new List<Vector3>();

        for (int i = 0; i < pointList.Count - 1; i++) {
            currentList.Add(pointList[i]);

            float distance = Vector3.Distance(pointList[i], pointList[i + 1]);

            if (distance > threshold) {
                separated.Add(currentList);
                currentList = new List<Vector3>();
            }
        }

        currentList.Add(pointList[pointList.Count - 1]);
        separated.Add(currentList);

        return separated;
    }

}

// https://www.youtube.com/watch?v=ggmArUbRvC4
// https://www.youtube.com/watch?v=R9I9prRUiEo
// https://discussions.unity.com/t/create-texture-from-current-camera-view/86847/2