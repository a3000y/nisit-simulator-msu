#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
namespace NisitSimulator.EditorTools
{
    public static class OnboardingCapture
    {
        public static void Capture(string name)
        {
            var cam=Camera.main; var oldTexture=cam.targetTexture; float aspect=cam.aspect;
            var oldActive=RenderTexture.active;
            var rt=new RenderTexture(1280,720,24); rt.Create();
            var canvases=new List<Canvas>(); var cameras=new List<Camera>(); var distances=new List<float>();
            try {
                cam.targetTexture=rt; cam.aspect=1280f/720f;
                foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if(c.renderMode==RenderMode.ScreenSpaceOverlay) { canvases.Add(c); cameras.Add(c.worldCamera); distances.Add(c.planeDistance); c.renderMode=RenderMode.ScreenSpaceCamera; c.worldCamera=cam; c.planeDistance=1; }
                foreach(var c in canvases) { var sc=c.GetComponent<CanvasScaler>(); if(sc!=null) sc.SendMessage("Update"); }
                Canvas.ForceUpdateCanvases();
                foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if(c!=cam && c.enabled && c.targetTexture!=null)c.Render();
                cam.Render(); RenderTexture.active=rt;
                var tex=new Texture2D(1280,720,TextureFormat.RGB24,false); tex.ReadPixels(new Rect(0,0,1280,720),0,0); tex.Apply();
                System.IO.Directory.CreateDirectory("Assets/Screenshots"); System.IO.File.WriteAllBytes("Assets/Screenshots/"+name+".png",tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            } finally {
                for(int i=0;i<canvases.Count;i++) {canvases[i].renderMode=RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera=cameras[i]; canvases[i].planeDistance=distances[i];}
                cam.targetTexture=oldTexture; cam.aspect=aspect; RenderTexture.active=oldActive; rt.Release(); Object.DestroyImmediate(rt);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif