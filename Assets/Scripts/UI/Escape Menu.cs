using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
public class EscapeMenu : MonoBehaviour
{
    public string mainSceneName;

    private GameManager gm = null;
    public void Start()
    {
        if(gm == null)
        {
            gm = GameManager.Instance;
            if(gm == null)
            {
                Debug.Log("Null game manager");
                return;
            }
        }
    }
    public void Leave()
    {
        Debug.Log("Выход..."); 
        SceneManager.LoadScene(mainSceneName);
    }


    // Вызываем этот метод при нажатии на кнопку
    public void Resume()
    {
        GameObject thisObject = transform.gameObject;
        thisObject.SetActive(!thisObject.activeSelf);
        gm.Resume();
    }
}