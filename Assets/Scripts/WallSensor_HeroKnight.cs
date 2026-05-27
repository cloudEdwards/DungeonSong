using UnityEngine;

public class WallSensor_HeroKnight : Sensor_HeroKnight {

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (! other.CompareTag(TagEnum.Wall.ToString()))
        {
            return;
        }

        m_ColCount++;
    }

    protected override void OnTriggerExit2D(Collider2D other)
    {
        if (! other.CompareTag(TagEnum.Wall.ToString()))
        {
            return;
        }
        
        m_ColCount--;
    }
}
