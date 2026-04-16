using UnityEngine;

namespace BookshelfPorting.Runtime
{
    public static class BookshelfAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindFirstObjectByType<BookshelfState>() != null)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }

            var systems = new GameObject("BookshelfSystems");
            var state = systems.AddComponent<BookshelfState>();
            var materialFactory = systems.AddComponent<MaterialFactory>();
            var generator = systems.AddComponent<BookshelfGenerator>();
            var cameraController = systems.AddComponent<CameraController>();
            var interactionController = systems.AddComponent<BookInteractionController>();
            var runtimeDataStore = systems.AddComponent<BookshelfRuntimeDataStore>();
            var experienceManager = systems.AddComponent<BookshelfExperienceManager>();
            var environment = systems.AddComponent<EnvironmentSetup>();

            var camerasRoot = new GameObject("Cameras").transform;
            var overviewPosition = new Vector3(1.45f, 1.38f, -1.83f);
            var overviewTarget = new Vector3(-1.05f, 0.98f, 1.18f);
            var overviewAnchor = CreateAnchor(
                camerasRoot,
                "OverviewAnchor",
                overviewPosition,
                Quaternion.LookRotation((overviewTarget - overviewPosition).normalized, Vector3.up));
            var frontalAnchor = CreateAnchor(camerasRoot, "FrontalAnchor", new Vector3(0f, 0.95f, -0.95f), Quaternion.LookRotation(new Vector3(0f, -0.1f, 2.15f).normalized, Vector3.up));
            var whiteboardAnchor = CreateAnchor(
                camerasRoot,
                "WhiteboardAnchor",
                new Vector3(-0.92f, 1.18f, 0.12f),
                Quaternion.LookRotation(Vector3.left, Vector3.up));
            var notebookPosition = new Vector3(-0.12f, 0.72f, -1.28f);
            var notebookTarget = new Vector3(-0.80f, 0.56f, -0.94f);
            var notebookAnchor = CreateAnchor(
                camerasRoot,
                "NotebookAnchor",
                notebookPosition,
                Quaternion.LookRotation((notebookTarget - notebookPosition).normalized, Vector3.up));
            var bookViewAnchor = CreateAnchor(camerasRoot, "BookViewAnchor", new Vector3(0f, 0.88f, -0.15f), Quaternion.Euler(0f, 0f, 0f));

            var environmentRoot = new GameObject("Environment");
            var probeObject = new GameObject("ReflectionProbe");
            probeObject.transform.SetParent(environmentRoot.transform, false);
            probeObject.transform.position = new Vector3(0f, 1.2f, 0.5f);
            var probe = probeObject.AddComponent<ReflectionProbe>();

            cameraController.Configure(mainCamera, overviewAnchor, frontalAnchor, whiteboardAnchor, notebookAnchor, bookViewAnchor);
            generator.Configure(state, materialFactory);
            interactionController.Configure(state, generator, cameraController, materialFactory, mainCamera, experienceManager);
            runtimeDataStore.Configure(state, materialFactory);
            experienceManager.Configure(state, cameraController, interactionController, materialFactory, runtimeDataStore);
            environment.Configure(probe, null);

            mainCamera.transform.position = overviewAnchor.position;
            mainCamera.transform.rotation = overviewAnchor.rotation;

            environment.Apply();
            generator.Generate();
            AlignNotebookAnchor(notebookAnchor);
            cameraController.MoveToMode(CameraMode.Overview);
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = position;
            anchor.rotation = rotation;
            return anchor;
        }

        private static void AlignNotebookAnchor(Transform notebookAnchor)
        {
            if (notebookAnchor == null)
            {
                return;
            }

            var notebookScreenController = Object.FindFirstObjectByType<NotebookScreenController>();
            var screen = notebookScreenController != null
                ? notebookScreenController.transform.Find("Screen")
                : null;
            if (screen == null)
            {
                return;
            }

            var screenCenterTransform = screen.Find("ScreenCenter");
            var cameraFocusPoint = screen.Find("CameraFocusPoint");
            var screenCenter = screenCenterTransform != null
                ? screenCenterTransform.position
                : screen.TransformPoint(new Vector3(0f, 0.068f, 0.014f));
            var anchorPosition = cameraFocusPoint != null
                ? cameraFocusPoint.position
                : screen.TransformPoint(new Vector3(0f, -0.58f, 0.024f));

            notebookAnchor.position = anchorPosition;
            notebookAnchor.rotation = Quaternion.LookRotation((screenCenter - anchorPosition).normalized, Vector3.up);
        }
    }
}
