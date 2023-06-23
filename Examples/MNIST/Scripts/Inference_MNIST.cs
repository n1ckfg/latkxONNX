using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Barracuda;

public class Inference_MNIST : MonoBehaviour {

    public Texture2D texture;
    public NNModel nnModel;

    private Model model;
    private IWorker worker;
    
    [Serializable]
    public struct Prediction {
        public int predictedValue;
        public float[] predicted;

        public void SetPrediction(Tensor t) {
            predicted = t.AsFloats();
            predictedValue = Array.IndexOf(predicted, predicted.Max());
            Debug.Log($"Predicted {predictedValue}");
        }
    }

    public Prediction prediction;

    private void Start() {
        model = ModelLoader.Load(nnModel);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
        prediction = new Prediction();
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            var channelCount = 1; // 1 = grayscale, 3 = rgb, 4 = rgba
            var inputX = new Tensor(texture, channelCount);
            Tensor outputY = worker.Execute(inputX).PeekOutput();
            inputX.Dispose(); // Barracuda objects are not GC'd
            prediction.SetPrediction(outputY);
        }
    }

   private void OnDestroy() {
        worker?.Dispose();
   }

}

// https://www.youtube.com/watch?v=ggmArUbRvC4
