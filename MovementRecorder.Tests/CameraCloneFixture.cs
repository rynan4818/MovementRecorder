using System;
using UnityEngine;

// Models component activation and serialized references, not Unity's native renderer or frame ordering.
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class RequireComponent : Attribute
    {
        public Type m_Type0, m_Type1, m_Type2;
        public RequireComponent(Type required) { m_Type0 = required; }
    }
    public sealed class RenderTexture : Object { public bool Released; public void Release() { Released = true; } }
    public sealed class AudioListener : Behaviour { }
}

public sealed class BloomPrePassRenderDataSO : UnityEngine.Object
{
    public readonly Data data = new Data();
    public sealed class Data { public RenderTexture bloomPrePassRenderTexture; }
}
public class BloomPrePass : MonoBehaviour
{
    public enum Mode { RenderAndSetData, SetDataOnly }
    [SerializeField] protected BloomPrePassRenderDataSO _bloomPrePassRenderData;
    protected BloomPrePassRenderDataSO.Data _renderData;
    [SerializeField] protected Mode _mode;
    public void SetMode(Mode mode) { _mode = mode; }
    public void Awake() { _renderData = _bloomPrePassRenderData == null ? new BloomPrePassRenderDataSO.Data() : _bloomPrePassRenderData.data; }
    public void OnDestroy() { _renderData?.bloomPrePassRenderTexture?.Release(); }
}
public class MainEffectController : MonoBehaviour
{
    [SerializeField] public UnityEngine.Object _mainEffectContainer;
    private ImageEffectController _imageEffectController;
    private Action<RenderTexture> afterImageEffectEvent = null;
    public void RenderTestFrame() { afterImageEffectEvent?.Invoke(null); }
    public void OnEnable()
    {
        if (_imageEffectController == null) _imageEffectController = GetComponent<ImageEffectController>() ?? gameObject.AddComponent<ImageEffectController>();
        _imageEffectController.Owner = this;
    }
}
public sealed class ImageEffectController : MonoBehaviour { public MainEffectController Owner; }
public sealed class CameraDepthTextureMode : MonoBehaviour
{
    [SerializeField] public int _depthTextureMode;
    public void Awake() { GetComponent<Camera>().depthTextureMode = _depthTextureMode; }
}
