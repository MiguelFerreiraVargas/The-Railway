using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BackpackSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCarry playerCarry;

    [Header("Input")]
    [SerializeField] private InputActionReference storeAction;
    [SerializeField] private InputActionReference dropAction;

    [Header("Settings")]
    [SerializeField] private int maxItems = 10;

    private List<GameObject> backpack =
        new List<GameObject>();

    private void OnEnable()
    {
        if (storeAction != null)
        {
            storeAction.action.Enable();
            storeAction.action.performed += StoreItem;
        }

        if (dropAction != null)
        {
            dropAction.action.Enable();
            dropAction.action.performed += DropItem;
        }
    }

    private void OnDisable()
    {
        if (storeAction != null)
        {
            storeAction.action.performed -= StoreItem;
            storeAction.action.Disable();
        }

        if (dropAction != null)
        {
            dropAction.action.performed -= DropItem;
            dropAction.action.Disable();
        }
    }

    private void StoreItem(InputAction.CallbackContext context)
    {
        if (playerCarry == null)
            return;

        if (backpack.Count >= maxItems)
        {
            Debug.Log("Mochila cheia!");
            return;
        }

        GameObject item =
            playerCarry.GetHeldObject();

        if (item == null)
            return;

        backpack.Add(item);

        playerCarry.Drop();

        item.SetActive(false);

        Debug.Log(
            "Guardado: " +
            item.name +
            " | " +
            backpack.Count +
            "/" +
            maxItems
        );
    }

    private void DropItem(InputAction.CallbackContext context)
    {
        if (backpack.Count == 0)
        {
            Debug.Log("Mochila vazia!");
            return;
        }

        GameObject item =
            backpack[backpack.Count - 1];

        backpack.RemoveAt(
            backpack.Count - 1
        );

        item.SetActive(true);

        Transform player =
            playerCarry.transform;

        Vector3 dropPosition =
            player.position +
            player.forward * 1.5f;

        item.transform.position =
            dropPosition;

        Rigidbody rb =
            item.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
        }

        Debug.Log(
            "Retirado: " +
            item.name +
            " | " +
            backpack.Count +
            "/" +
            maxItems
        );
    }
}
