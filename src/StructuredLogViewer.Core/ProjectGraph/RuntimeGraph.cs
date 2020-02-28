﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

#nullable enable

namespace StructuredLogViewer.Core.ProjectGraph
{
    public class RuntimeGraph
    {
        [DebuggerDisplay("{DebuggerDisplayString()}")]
        public class RuntimeGraphNode
        {
            private SortedList<DateTime, RuntimeGraphNode>? sortedChildren;
            private IReadOnlyList<RuntimeGraphNode>? sortedChildrenCached;
            public Project Project { get; }
            public RuntimeGraphNode? Parent { get; internal set; }

            /// <summary>
            ///     Sorted by <see cref="Microsoft.Build.Logging.StructuredLogger.Project.StartTime" />
            /// </summary>
            public IReadOnlyList<RuntimeGraphNode> SortedChildren
            {
                get
                {
                    if (sortedChildren == null)
                    {
                        return ImmutableList<RuntimeGraphNode>.Empty;
                    }

                    sortedChildrenCached ??= sortedChildren.Values.ToImmutableList();
                    return sortedChildrenCached;
                }
            }

            public RuntimeGraphNode(Project p)
            {
                Project = p;
            }

            public string DebuggerDisplayString()
            {
                var sb = new StringBuilder();

                sb.Append($"ReferenceCount: {SortedChildren.Count}, {Project.DebuggerDisplayString()}");

                return sb.ToString();
            }

            internal void AddChild(RuntimeGraphNode child)
            {
                if (sortedChildren == null)
                {
                    sortedChildren = new SortedList<DateTime, RuntimeGraphNode>();
                }

                sortedChildren.Add(child.Project.StartTime, child);
                sortedChildrenCached = null;
            }
        }

        /// <summary>
        ///     Sorted by <see cref="Project.StartTime" />
        /// </summary>
        public IReadOnlyList<RuntimeGraphNode> SortedRoots { get; }

        public ICollection<RuntimeGraphNode> Nodes { get; }

        private RuntimeGraph(IReadOnlyList<RuntimeGraphNode> sortedRoots, ICollection<RuntimeGraphNode> nodes)
        {
            SortedRoots = sortedRoots;
            Nodes = nodes;
        }

        public static RuntimeGraph FromBuild(Build build)
        {
            var projects = build.FindChildrenRecursive<Project>();
            var runtimeNodes = new ConcurrentDictionary<Project, RuntimeGraphNode>(1, projects.Count);

            foreach (var project in projects)
            {
                var node = GetOrAddNode(runtimeNodes, project);

                var projectParent = project.GetNearestParent<Project>();

                if (projectParent != null)
                {
                    var nodeParent = GetOrAddNode(runtimeNodes, projectParent);
                    node.Parent = nodeParent;
                    nodeParent.AddChild(node);
                }
            }

            var roots = runtimeNodes.Values.Where(n => n.Parent == null).ToArray();

            return new RuntimeGraph(roots, runtimeNodes.Values);

            RuntimeGraphNode GetOrAddNode(ConcurrentDictionary<Project, RuntimeGraphNode> runtimeGraphNodes, Project projectInvocation)
            {
                return runtimeGraphNodes.GetOrAdd(projectInvocation, p => new RuntimeGraphNode(p));
            }
        }
    }
}
