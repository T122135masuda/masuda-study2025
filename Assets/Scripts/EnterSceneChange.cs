using UnityEngine;
using UnityEngine.SceneManagement;

public class EnterSceneChange : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene("home");
        }
    }
}
