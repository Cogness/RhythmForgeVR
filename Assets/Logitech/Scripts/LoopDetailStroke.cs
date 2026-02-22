using System.Collections.Generic;
using UnityEngine;

public class LoopDetailStroke : MonoBehaviour
{
    private LoopSound _loopSound;
    private List<float> _addedSpikes;

    public void Initialize(LoopSound loopSound, List<float> addedSpikes)
    {
        _loopSound = loopSound;
        _addedSpikes = addedSpikes;
    }

    private void OnDestroy()
    {
        if (_loopSound != null && _addedSpikes != null && _addedSpikes.Count > 0)
        {
            _loopSound.RemoveSpikes(_addedSpikes);
        }
    }
}
