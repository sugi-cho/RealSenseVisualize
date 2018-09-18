using UnityEngine;
using UnityEngine.Events;

public class KeyControl : MonoBehaviour {

    public KeyEventPair[] keyEvents;

    void Update()
    {
        foreach (var k in keyEvents)
            k.Invoke();
    }

[System.Serializable]
    public struct KeyEventPair
    {
        public KeyCode key;
        public UnityEvent keyEvent;

        public void Invoke()
        {
            if (Input.GetKeyDown(key))
                keyEvent.Invoke();
        }
    }
}
