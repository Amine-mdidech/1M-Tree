using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CameraPanControllerTests
{
    private GameObject cameraGO;
    private Camera testCamera;
    private GameObject controllerGO;
    private CameraPanController controller;

    [SetUp]
    public void SetUp()
    {
        cameraGO = new GameObject("TestCamera");
        testCamera = cameraGO.AddComponent<Camera>();
        testCamera.orthographic = true;
        testCamera.orthographicSize = 5f;

        controllerGO = new GameObject("TestCameraPanController");
        controller = controllerGO.AddComponent<CameraPanController>();

        SetPrivateField(controller, "targetCamera", testCamera);
        SetPrivateField(controller, "mapHalfWidth", 30f);
        SetPrivateField(controller, "mapHalfHeight", 30f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(cameraGO);
        Object.DestroyImmediate(controllerGO);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    private Vector3 InvokeClamp(Vector3 position)
    {
        MethodInfo method = typeof(CameraPanController).GetMethod("ClampToMapBounds", BindingFlags.NonPublic | BindingFlags.Instance);
        return (Vector3)method.Invoke(controller, new object[] { position });
    }

    [Test]
    public void ClampToMapBounds_WithinBounds_IsUnchanged()
    {
        Vector3 result = InvokeClamp(new Vector3(2f, 3f, -10f));

        Assert.AreEqual(2f, result.x, 0.001f);
        Assert.AreEqual(3f, result.y, 0.001f);
    }

    [Test]
    public void ClampToMapBounds_FarOutsideBounds_IsClampedToEdge()
    {
        Vector3 result = InvokeClamp(new Vector3(1000f, -1000f, -10f));

        float halfViewportHeight = testCamera.orthographicSize;
        float halfViewportWidth = halfViewportHeight * testCamera.aspect;
        float expectedMaxX = 30f - halfViewportWidth;
        float expectedMinY = -(30f - halfViewportHeight);

        Assert.AreEqual(expectedMaxX, result.x, 0.01f);
        Assert.AreEqual(expectedMinY, result.y, 0.01f);
    }

    [Test]
    public void ClampToMapBounds_PreservesZCoordinate()
    {
        Vector3 result = InvokeClamp(new Vector3(0f, 0f, -10f));

        Assert.AreEqual(-10f, result.z, 0.001f);
    }
}
