using UnityEngine;
using UnityEngine.Events;

public class DriftRunManager : MonoBehaviour
{
    public float runDuration = 90f;
    public UnityEvent OnRunStart;
    public UnityEvent OnRunEnd;

    public bool IsRunning { get; private set; }
    public float TimeLeft { get; private set; }

    private void Start()
    {
        StartRun();
    }

    public void StartRun()
    {
            TimeLeft = runDuration;
            IsRunning = true;
            OnRunStart?.Invoke();
            Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!IsRunning) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            IsRunning = false;
            OnRunEnd?.Invoke();
            // here you can show "Run Over" UI
        }

        //if (Input.GetKeyDown(KeyCode.R) || GamepadRestartPressed())
        //{
            // manual restart
            //StartRun();
            //UnityEngine.SceneManagement.SceneManager.LoadScene(
                //UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        //}
    }

    bool GamepadRestartPressed()
    {
        // optional, you can wire this to the new Input System later
        return false;
    }
}
