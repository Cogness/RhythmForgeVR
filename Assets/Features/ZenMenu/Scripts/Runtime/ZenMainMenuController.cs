using UnityEngine;

[DisallowMultipleComponent]
public class ZenMainMenuController : MonoBehaviour
{
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private Camera _headCamera;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _penDockAnchor;
    [SerializeField] private Transform _mxInkRoot;

    private void Awake()
    {
        if (_headCamera == null)
        {
            _headCamera = Camera.main;
        }

        if (_playerRigRoot == null && _headCamera != null)
        {
            _playerRigRoot = _headCamera.transform.root;
        }

        AlignPlayerRig();
        PlacePenDockObject();
    }

    private void AlignPlayerRig()
    {
        if (_playerRigRoot == null || _headCamera == null || _spawnPoint == null)
        {
            return;
        }

        Vector3 headOffset = _headCamera.transform.position - _playerRigRoot.position;
        headOffset.y = 0f;
        _playerRigRoot.position = _spawnPoint.position - headOffset;

        Vector3 projectedForward = Vector3.ProjectOnPlane(_headCamera.transform.forward, Vector3.up);
        Vector3 targetForward = Vector3.ProjectOnPlane(_spawnPoint.forward, Vector3.up);
        if (projectedForward.sqrMagnitude > 0.001f && targetForward.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.SignedAngle(projectedForward, targetForward, Vector3.up);
            _playerRigRoot.Rotate(Vector3.up, angle, Space.World);
        }
    }

    private void PlacePenDockObject()
    {
        if (_mxInkRoot == null || _penDockAnchor == null)
        {
            return;
        }

        _mxInkRoot.position = _penDockAnchor.position;
        _mxInkRoot.rotation = _penDockAnchor.rotation;
    }
}
