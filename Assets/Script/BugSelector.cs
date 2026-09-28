using UnityEngine;
using UnityEngine.InputSystem;

public class BugSelector : MonoBehaviour
{
    private const int ObjectCaseCount = 6;

    private GameObject bug1;
    private GameObject bug2;
    private GameObject tangentBug;

    private Bug1 bug1Script;
    private Bug2 bug2Script;
    private BugTangent tangentBugScript;
    private Transform goal;

    private readonly ObjectControl[] objectCases = new ObjectControl[ObjectCaseCount];

    private int selectedBug = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSelector()
    {
        GameObject selectorObject = new GameObject("BugSelector");
        selectorObject.AddComponent<BugSelector>();
    }

    private void Awake()
    {
        Bug1 bug1Component = FindAnyObjectByType<Bug1>(FindObjectsInactive.Include);
        Bug2 bug2Component = FindAnyObjectByType<Bug2>(FindObjectsInactive.Include);
        BugTangent tangentBugComponent = FindAnyObjectByType<BugTangent>(FindObjectsInactive.Include);

        bug1 = bug1Component != null ? bug1Component.gameObject : null;
        bug2 = bug2Component != null ? bug2Component.gameObject : null;
        tangentBug = tangentBugComponent != null ? tangentBugComponent.gameObject : null;

        bug1Script = bug1Component;
        bug2Script = bug2Component;
        tangentBugScript = tangentBugComponent;
        goal = bug1Component != null ? bug1Component.goal : null;

        for (int index = 0; index < ObjectCaseCount; index++)
        {
            GameObject caseObject = GameObject.Find("ObjectCase" + (index + 1));
            objectCases[index] = caseObject != null ? caseObject.GetComponent<ObjectControl>() : null;
        }

        SelectBug(1);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Quit();
            return;
        }

        if (Keyboard.current.aKey.wasPressedThisFrame)
            SelectBug(1);
        else if (Keyboard.current.sKey.wasPressedThisFrame)
            SelectBug(2);
        else if (Keyboard.current.dKey.wasPressedThisFrame)
            SelectBug(3);

        if (selectedBug == 1 || selectedBug == 2)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                SelectObjectCase(1);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
                SelectObjectCase(2);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
                SelectObjectCase(3);
            else if (Keyboard.current.digit4Key.wasPressedThisFrame)
                SelectObjectCase(4);
        }
        else if (selectedBug == 3)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                SelectObjectCase(5);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
                SelectObjectCase(6);
        }
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SelectBug(int bugNumber)
    {
        selectedBug = bugNumber;
        SetBugActive(bug1, bugNumber == 1);
        SetBugActive(bug2, bugNumber == 2);
        SetBugActive(tangentBug, bugNumber == 3);

        if (bugNumber == 1 || bugNumber == 2)
            SelectObjectCase(1);
        else if (bugNumber == 3)
            SelectObjectCase(5);
    }

    private void SelectObjectCase(int caseNumber)
    {
        ObjectControl activeCase = null;

        for (int index = 0; index < ObjectCaseCount; index++)
        {
            ObjectControl objectCase = objectCases[index];
            if (objectCase == null)
                continue;

            bool isActive = index + 1 == caseNumber;
            objectCase.meshEnabled = isActive;
            objectCase.colliderEnabled = isActive;
            objectCase.ApplySettings();

            if (isActive)
                activeCase = objectCase;
        }

        if (activeCase != null)
            MoveToCase(activeCase);
    }

    private void MoveToCase(ObjectControl activeCase)
    {
        GameObject activeBug;
        if (selectedBug == 1)
            activeBug = bug1;
        else if (selectedBug == 2)
            activeBug = bug2;
        else
            activeBug = tangentBug;

        if (activeBug == null)
            return;

        if (activeCase.bugPoint != null)
            activeBug.transform.position = activeCase.bugPoint.position;

        if (goal != null && activeCase.goalPoint != null)
            goal.position = activeCase.goalPoint.position;

        if (selectedBug == 1)
        {
            if (bug1Script != null)
                bug1Script.ResetToStop();
        }
        else if (selectedBug == 2)
        {
            if (bug2Script != null)
                bug2Script.ResetToStop();
        }
        else
        {
            if (tangentBugScript != null)
                tangentBugScript.ResetToStop();
        }
    }

    private static void SetBugActive(GameObject bug, bool isActive)
    {
        if (bug == null)
            return;

        bug.SetActive(isActive);
        Renderer[] renderers = bug.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
            renderer.enabled = isActive;

        Collider[] colliders = bug.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
            collider.enabled = isActive;

        MonoBehaviour[] scripts = bug.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
            script.enabled = isActive;
    }
}
