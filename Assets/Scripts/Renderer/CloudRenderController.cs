using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using System.Collections;

[ExecuteAlways]
public class CloudRenderController : MonoBehaviour
{
    // Settings
    [Header("Controller Settings")]
    [SerializeField]
    private ComputeShader computeShader;
    [SerializeField]
    private Shader skyShader;
    [SerializeField]
    private float updateInterval = 8.0f;
    [SerializeField]
    private float blendDuration = 2.0f;

    // Members
    private Camera cam;
    private RenderTexture[] renderTextures = new RenderTexture[2];
    private int currentRTIndex = 0;
    private Material oldSkyMaterial;
    private Material skyMaterial;

    private void InitializeRenderTextures()
    {
        for (int i = 0; i < 2; i++)
        {
            renderTextures[i] = new RenderTexture(1024, 1024, 0, GraphicsFormat.R16G16B16A16_UNorm);
            renderTextures[i].dimension = TextureDimension.Cube;
            renderTextures[i].hideFlags = HideFlags.HideAndDontSave;
            renderTextures[i].useMipMap = false;
            renderTextures[i].autoGenerateMips = false;
            renderTextures[i].enableRandomWrite = true;

            renderTextures[i].Create();
        }
    }

    private void InitializeCamera()
    {
        GameObject camObj = new GameObject("CloudCubemapCam", typeof(Camera));
        camObj.hideFlags = HideFlags.HideAndDontSave;
        camObj.transform.position = Vector3.zero;
        camObj.transform.rotation = Quaternion.identity;
        cam = camObj.GetComponent<Camera>();

        Camera mainCam = Camera.main;

        if (mainCam == null)
        {
            mainCam = GameObject.FindWithTag("MainCamera")?.GetComponent<Camera>();
        }

        if (mainCam != null)
        {
            cam.fieldOfView = mainCam.fieldOfView;
            cam.nearClipPlane = mainCam.nearClipPlane;
            cam.farClipPlane = mainCam.farClipPlane;
        }
        else
        {
            cam.fieldOfView = 60;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000;
        }

        cam.enabled = false;
        cam.cullingMask = 1 << LayerMask.NameToLayer("Clouds");
    }

    void Start()
    {
        if (!skyShader)
        {
            Debug.LogWarning("[CloudRenderController]: No skybox shader found. Aborting.");
            return;
        }

        if (!computeShader)
        {
            Debug.LogWarning("[CloudRenderController]: No compute shader found. Aborting.");
            return;
        }

        if (blendDuration > updateInterval)
        {
            blendDuration = updateInterval;
        }

        InitializeRenderTextures();
        InitializeCamera();

        skyMaterial = new Material(skyShader);
        if (skyMaterial)
        {
            skyMaterial.SetTexture("_CurrentSkybox", renderTextures[0]);
            skyMaterial.SetTexture("_HistorySkybox", renderTextures[1]);
            skyMaterial.SetFloat("_BlendFactor", 1.0f);

            oldSkyMaterial = RenderSettings.skybox;
            RenderSettings.skybox = skyMaterial;
        }

        StartCoroutine(UpdateCubemap());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (renderTextures != null)
        {
            renderTextures[0]?.Release();
            renderTextures[1]?.Release();
        }

        RenderSettings.skybox = oldSkyMaterial;

        if (Application.isPlaying)
        {
            Destroy(cam);
            Destroy(skyMaterial);
        }
        else
        {
            DestroyImmediate(cam);
            DestroyImmediate(skyMaterial);
        }
    }

    IEnumerator UpdateCubemap()
    {
        while (true)
        {
            if (!skyMaterial || !renderTextures[0] || !renderTextures[1])
            {
                Debug.LogWarning("[CloudRenderController]: Invalid material or render textures. Aborting update.");
                yield return new WaitForSeconds(updateInterval);
                continue;
            }

            RenderTexture writeRT = renderTextures[currentRTIndex];
            for (int i = 0; i < 6; i++)
            {
                cam.RenderToCubemap(writeRT, 1 << i);
            }

            Debug.Log("[CloudRenderController]: Finished rendering skybox");
            RenderTexture historyRT = renderTextures[(currentRTIndex + 1) % 2];
            skyMaterial.SetTexture("_CurrentSkybox", writeRT);
            skyMaterial.SetTexture("_HistorySkybox", historyRT);

            float elapsedTime = 0.0f;
            while (elapsedTime < blendDuration)
            {
                float blendFactor = Mathf.Clamp01(elapsedTime / blendDuration);
                skyMaterial.SetFloat("_BlendFactor", blendFactor);
                elapsedTime += Time.deltaTime;

                yield return null;
            }
            skyMaterial.SetFloat("_BlendFactor", 1.0f);
            currentRTIndex = (currentRTIndex + 1) % 2;

            float remainderWait = updateInterval - blendDuration;
            if (remainderWait > 0)
            {
                yield return new WaitForSeconds(remainderWait);
            }
        }
    }
}
