// https://www.youtube.com/watch?v=ggmArUbRvC4

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawOnTexture : MonoBehaviour {

    public Texture2D tex;

    private void Update() {
        if (Camera.main == null) throw new Exception("Cannot find main camera.");

        if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit)) return;

        if (hit.collider.transform != transform) return;

        Vector2 uv = hit.textureCoord;
        uv.x *= tex.width;
        uv.y *= tex.height;
        Color col = Input.GetMouseButton(0) ? Color.white : Color.black;

        tex.SetPixel((int)uv.x, (int)uv.y, col);
        tex.Apply();
    }

}
