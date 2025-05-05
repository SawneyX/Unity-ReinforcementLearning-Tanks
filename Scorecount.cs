using UnityEngine;
using TMPro;

public class Scorecount : MonoBehaviour
{
    [SerializeField] private TMP_Text score;

    public GameObject AITank;
    TankAgent2 tankAgent;

    // Start is called before the first frame update
    void Start()
    {
        tankAgent = AITank.GetComponent<TankAgent2>();
    }

    // Update is called once per frame
    void Update()
    {

        float topTime = tankAgent.topTime;
        if (topTime == 10000) topTime = 0;
        score.text = "Score: " + tankAgent.score.ToString() + "\nTime: " + tankAgent.timeElapsed + "s" + "\n\nBest: " + topTime + "s";
    }
}
