using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class TechTreeConnectorView : VisualElement
{
    public struct Connection
    {
        public Vector2 From;
        public Vector2 To;
        public Color Color;
    }

    private readonly List<Connection> connections = new List<Connection>();

    public TechTreeConnectorView()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += OnGenerateVisualContent;
    }

    public void SetConnections(List<Connection> newConnections)
    {
        connections.Clear();
        connections.AddRange(newConnections);
        MarkDirtyRepaint();
    }

    private static readonly Color PipeShadowColor = new Color(0.07f, 0.05f, 0.04f, 0.5f);

    private void OnGenerateVisualContent(MeshGenerationContext context)
    {
        if (connections.Count == 0)
        {
            return;
        }

        Painter2D painter = context.painter2D;
        painter.lineCap = LineCap.Round;

        foreach (Connection connection in connections)
        {
            painter.lineWidth = 9f;
            painter.strokeColor = PipeShadowColor;
            DrawConnectionPath(painter, connection);
            painter.Stroke();

            painter.lineWidth = 5f;
            painter.strokeColor = connection.Color;
            DrawConnectionPath(painter, connection);
            painter.Stroke();
        }
    }

    private static void DrawConnectionPath(Painter2D painter, Connection connection)
    {
        painter.BeginPath();
        painter.MoveTo(connection.From);

        float midX = (connection.From.x + connection.To.x) * 0.5f;
        painter.LineTo(new Vector2(midX, connection.From.y));
        painter.LineTo(new Vector2(midX, connection.To.y));
        painter.LineTo(connection.To);
    }
}
