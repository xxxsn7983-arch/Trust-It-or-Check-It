using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using UnityEngine;

public class PlayKitOfficialStyleSTTTest : MonoBehaviour
{
    [SerializeField] private PlayKit_Transcribe transcriber;
    [SerializeField] private float maxDurationSeconds = 8f;
    [SerializeField] private string language = "zh";

    private void Awake()
    {
        if (transcriber == null)
        {
            transcriber = GetComponent<PlayKit_Transcribe>();
        }
    }

    private async void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            await RunOfficialStyleTest();
        }
    }

    public async UniTask RunOfficialStyleTest()
    {
        if (transcriber == null)
        {
            Debug.LogWarning("[PlayKit Official STT Test] PlayKit_Transcribe is not assigned.", this);
            return;
        }

        Debug.Log("[PlayKit Official STT Test] Recording starts. Speak now.", this);
        string text = await transcriber.RecordAndTranscribeAsync(maxDurationSeconds, language);
        Debug.Log("[PlayKit Official STT Test] Result = " + text, this);
    }
}
