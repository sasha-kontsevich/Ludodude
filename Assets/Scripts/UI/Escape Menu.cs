using UnityEngine;
using UnityEngine.SceneManagement;
public class EscapeMenu : MonoBehaviour
{
    public string mainSceneName;
    public void Leave()
    {
        Debug.Log("Выход..."); 
        SceneManager.LoadScene(mainSceneName);
    }


    // Вызываем этот метод при нажатии на кнопку
    public void Resume()
    {
        //Hide menu
    }
}