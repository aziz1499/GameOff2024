

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerConeDetection : MonoBehaviour
{
    [SerializeField] private GameObject headLightOutline; // indique si la lumière est activée

    void OnTriggerEnter(Collider col)
    {
        HandleEnemyDetection(col);
    }

    void OnTriggerStay(Collider col)
    {
        HandleEnemyDetection(col);
    }

    
    private void HandleEnemyDetection(Collider col) // ---->Nouvelle méthode pour éviter duplication de code
    {
        if (!headLightOutline.activeSelf) return;

        if (col.CompareTag("Enemy"))
        {
            Vector3 direction = (col.transform.position - transform.position).normalized; //--> Utilisation du vecteur
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, 50))
            {
                if (hit.collider.gameObject == col.gameObject)
                {
                    PatrolNavigation patrol = col.GetComponent<PatrolNavigation>();
                    if (patrol != null)
                    {
                        patrol.BecomeStunned();
                    }
                }
            }
        }
    }
}
