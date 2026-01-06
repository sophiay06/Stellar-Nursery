using UnityEngine;
using UnityEngine.SceneManagement;

public class SessionEndActions : MonoBehaviour
{
    // Option A: End but stay in the same scene
    public void EndSessionNoOp()
    {
        Debug.Log("Session ended (no-op).");
    }

    // Option B: Load a menu/ending scene
    public void LoadScene(string sceneName)
    {
        Debug.Log("Session ended → loading scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    // Option C: Quit the app
    public void QuitApp()
    {
        Debug.Log("Session ended → quit app");
        Application.Quit();
    }
}
