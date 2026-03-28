using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{
    public void Exit()
    {
        Debug.Log("Выход..."); 
        Application.Quit();  
    }

    public string sceneName;

    // Вызываем этот метод при нажатии на кнопку
    public void Play()
    {
        SceneManager.LoadScene(sceneName);
    }
}