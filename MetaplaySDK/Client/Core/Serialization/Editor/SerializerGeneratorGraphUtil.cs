// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Core.Serialization
{
    /// <summary>
    /// Utilities for extracting information about the type graph that the serializer generator deals with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// We deal with directed graphs here.
    /// In this code, "neighbor" is used as a synonym for "direct successor":
    /// node Y is a neighbor of X if there is an edge from X to Y.
    /// Note that neighborness is not a symmetric relation.
    /// "Reverse neighbor" is used to mean "direct predecessor".
    /// </para>
    /// <para>
    /// In this code, graphs are usually represented as adjacency dictionaries
    /// of the form <c>MetaDictionary&lt;TNode, List&lt;TNode&gt;&gt;</c>,
    /// which maps each node to a list of its neighbors.
    /// All of the graph's nodes must be present as keys in the dictionary,
    /// even those that have 0 neighbors; this way we don't need a separate list
    /// of the nodes of the graph.
    /// The dictionary is ordered, because it matters in some contexts,
    /// and where it doesn't matter it's so by convention.
    /// </para>
    /// <para>
    /// While the type graph's nodes are specifically <see cref="Type"/>s,
    /// this code is written generically with the TNode type parameters, for clarity.
    /// </para>
    /// </remarks>
    internal static class SerializerGeneratorGraphUtil
    {
        /// <summary>
        /// Information extracted about a graph, by <see cref="ResolveGraphInfo"/>.
        /// </summary>
        internal class GraphInfo<TNode>
        {
            /// <summary>
            /// The nodes that belong in at least 1 cycle in the graph.
            /// </summary>
            public OrderedSet<TNode> NodesBelongingToCycles;
            /// <summary>
            /// The length of the longest path in the "component graph", which is essentially
            /// the original graph "collapsed" into its strongly connected components:
            /// the "component graph" is a graph whose nodes are the strongly-connected components
            /// of the original graph, and where node C1 is neighbor of node C0 if, in the original graph,
            /// some node in the component C1 is neighbor of some node in the component C0;
            /// except that neighbor relations _within_ a component are omitted (the component graph does
            /// not contain loops).
            /// </summary>
            public int MaxPathLengthInComponentGraph;
        }

        /// <summary>
        /// Resolve <see cref="GraphInfo{TNode}"/> from the given graph.
        /// The graph is defined by the adjacency dictionary <paramref name="nodeToNeighbors"/>;
        /// see remarks on <see cref="SerializerGeneratorGraphUtil"/> for more info.
        /// </summary>
        internal static GraphInfo<TNode> ResolveGraphInfo<TNode>(MetaDictionary<TNode, List<TNode>> nodeToNeighbors)
        {
            // Use Tarjan's algorithm to resolve the strongly connected components of the input graph.
            // From the components, it'll then be easy to figure out which types belong to cycles.
            List<List<TNode>> components = TarjansStronglyConnectedComponentsAlgorithm.ResolveComponents(nodeToNeighbors);
            // Tarjan's algorithm produces the components in reverse topological order.
            // For convenience, we reverse it here, producing non-reversed topological order.
            components.Reverse();

            // Produce the "component graph" by collapsing each strongly connected component to a node (int, index in the components list).
            MetaDictionary<int, List<int>> componentGraph = CollapseComponentsToGraph(components, nodeToNeighbors);

            return new GraphInfo<TNode>
            {
                NodesBelongingToCycles = ResolveNodesBelongingToCycles(components, nodeToNeighbors),
                // Compute the max path length in the component graph (which is in topological order thanks to Tarjan's algorithm).
                MaxPathLengthInComponentGraph = ComputeMaxPathLengthInTopologicallyOrderedAcyclicGraph(componentGraph),
            };
        }

        /// <summary>
        /// Implements Tarjan's strongly connected components algorithm.
        /// <see cref="ResolveComponents"/> is the public API.
        /// </summary>
        /// <remarks>
        /// See Tarjan's algorithm in literature (or Wikipedia) for explanation of the actual algorithm.
        /// </remarks>
        static class TarjansStronglyConnectedComponentsAlgorithm
        {
            /// <summary>
            /// Given a graph, resolve its strongly-connected components using Tarjan's algorithm.
            /// In the returned list, each element is a component represented as a list of the nodes in the component.
            /// By Tarjan's algorithm, the ordering of the components is reverse topological order.
            /// </summary>
            public static List<List<TNode>> ResolveComponents<TNode>(MetaDictionary<TNode, List<TNode>> nodeToNeighbors)
            {
                ICollection<TNode> nodes = nodeToNeighbors.Keys;

                MetaDictionary<TNode, NodeData> nodeDatas = new(capacity: nodes.Count);
                // nodeStack, reused across invocations of FindComponentsImpl to reduce allocation
                Stack<TNode> nodeStackRecycled = new();

                List<List<TNode>> components = new();

                foreach (TNode node in nodes)
                {
                    if (nodeDatas.ContainsKey(node))
                        continue;

                    FindComponentsImpl(node, nodeToNeighbors, nodeDatas, nodeStackRecycled, components);
                    MetaDebug.Assert(nodeStackRecycled.Count == 0, "Node stack must be empty at this point");
                }

                return components;
            }

            class NodeData
            {
                public int Number;
                public int LowLink;
                public bool IsOnNodeStack;
            }

            static NodeData FindComponentsImpl<TNode>(
                TNode currentNode,
                MetaDictionary<TNode, List<TNode>> nodeToNeighbors,
                MetaDictionary<TNode, NodeData> nodeDatas,
                Stack<TNode> nodeStack,
                List<List<TNode>> componentsOutput)
            {
                int nodeNumber = nodeDatas.Count;
                NodeData currentNodeData = new NodeData
                {
                    Number = nodeNumber,
                    LowLink = nodeNumber,
                    IsOnNodeStack = true,
                };
                nodeDatas.Add(currentNode, currentNodeData);
                nodeStack.Push(currentNode);

                foreach (TNode neighbor in nodeToNeighbors[currentNode])
                {
                    if (!nodeDatas.ContainsKey(neighbor))
                    {
                        NodeData neighborData = FindComponentsImpl(neighbor, nodeToNeighbors, nodeDatas, nodeStack, componentsOutput);
                        currentNodeData.LowLink = System.Math.Min(currentNodeData.LowLink, neighborData.LowLink);
                    }
                    else
                    {
                        NodeData neighborData = nodeDatas[neighbor];
                        if (neighborData.IsOnNodeStack)
                            currentNodeData.LowLink = System.Math.Min(currentNodeData.LowLink, neighborData.Number);
                    }
                }

                if (currentNodeData.LowLink == currentNodeData.Number)
                {
                    List<TNode> component = new();
                    while (true)
                    {
                        TNode poppedNode = nodeStack.Pop();
                        nodeDatas[poppedNode].IsOnNodeStack = false;
                        component.Add(poppedNode);

                        if (poppedNode.Equals(currentNode))
                            break;
                    }

                    componentsOutput.Add(component);
                }

                return currentNodeData;
            }
        }

        /// <summary>
        /// Given the strongly-connected components of a graph, and the adjacency dictionary of the graph,
        /// returns the set of nodes that are part of at least one cycle in the graph.
        /// </summary>
        static OrderedSet<TNode> ResolveNodesBelongingToCycles<TNode>(List<List<TNode>> stronglyConnectedComponents, MetaDictionary<TNode, List<TNode>> nodeToNeighbors)
        {
            OrderedSet<TNode> nodesInCycles = new();

            foreach (List<TNode> component in stronglyConnectedComponents)
            {
                // A node is part of a cycle iff:
                // - it is in a strongly-connected component that has more than 1 node
                // - or it is its own neighbor (a self-cycle)

                if (component.Count > 1)
                {
                    foreach (TNode node in component)
                        nodesInCycles.Add(node);
                }
                else
                {
                    TNode node = component[0];
                    if (nodeToNeighbors[node].Contains(node))
                        nodesInCycles.Add(node);
                }
            }

            return nodesInCycles;
        }

        /// <summary>
        /// Given components of a graph, and the adjacency dictionary of the graph,
        /// returns the "component graph", which is a graph representing the relations of the components
        /// (see comment on <see cref="GraphInfo{TNode}.MaxPathLengthInComponentGraph"/> for more info).
        /// <para>
        /// Each component in the returned adjacency dictionary is represented by an
        /// <see cref="int"/> which is the index of the component in the input <paramref name="components"/>.
        /// </para>
        /// </summary>
        static MetaDictionary<int, List<int>> CollapseComponentsToGraph<TNode>(List<List<TNode>> components, MetaDictionary<TNode, List<TNode>> nodeToNeighbors)
        {
            ICollection<TNode> nodes = nodeToNeighbors.Keys;

            // Create a mapping from each node to the (index of the) component that contains the node.
            MetaDictionary<TNode, int> nodeToComponentIndex = new(capacity: nodes.Count);
            foreach ((List<TNode> component, int componentIndex) in components.ZipWithIndex())
            {
                foreach (TNode node in component)
                    nodeToComponentIndex.Add(node, componentIndex);
            }

            // Create the adjacency dictionary of the resulting component graph.
            MetaDictionary<int, List<int>> componentToNeighbors = new(capacity: components.Count);
            foreach ((List<TNode> component, int componentIndex) in components.ZipWithIndex())
            {
                OrderedSet<int> neighborComponents = new();

                // Another component is a neighbor of the current component
                // if some node in the other component is a neighbor of some node in the current component.
                // We ignore neighbor relations within a component (including self-referring nodes).

                foreach (TNode node in component)
                {
                    foreach (TNode neighborNode in nodeToNeighbors[node])
                    {
                        int neighborComponentIndex = nodeToComponentIndex[neighborNode];
                        if (neighborComponentIndex == componentIndex) // Ignore neighbor relations within a component.
                            continue;

                        neighborComponents.Add(neighborComponentIndex);
                    }
                }

                componentToNeighbors.Add(componentIndex, neighborComponents.ToList());
            }

            return componentToNeighbors;
        }

        /// <summary>
        /// Given an acyclic graph represented as an adjacency dictionary, which must be topologically ordered,
        /// computes the length of the longest path.
        /// </summary>
        static int ComputeMaxPathLengthInTopologicallyOrderedAcyclicGraph<TNode>(MetaDictionary<TNode, List<TNode>> nodeToNeighbors)
        {
            ICollection<TNode> nodes = nodeToNeighbors.Keys;

            // Dictionary for the reverse neighbor relations.
            // We initialize this with empty neighbor sets and then populate them as we go in the main loop below.
            MetaDictionary<TNode, OrderedSet<TNode>> nodeToReverseNeighbors = new(capacity: nodes.Count);
            foreach (TNode node in nodes)
                nodeToReverseNeighbors.Add(node, new OrderedSet<TNode>());

            // https://en.wikipedia.org/wiki/Longest_path_problem#Acyclic_graphs
            // - We'll calculate the "length of the longest path ending at node N" for each node
            // - Go over the nodes one by one, in the topological order
            // - At each node, get the maximum of the "length of the longest path ending at node X" for
            //   each reverse neighbor X of the current node. Add 1 to that maximum, producing
            //   the result for the current node.
            //   - Or if the current node has no reverse neighbors, then the result for this node is 0.
            // - Additionally, we populate nodeToReverseNeighbors's entries for upcoming nodes as we go.
            //   We know that the current node's neighbors are yet to be encountered by the loop,
            //   based on the assumption of topological ordering.

            MetaDictionary<TNode, int> maxPathLengthEndingAtNode = new(capacity: nodes.Count);

            foreach (TNode node in nodes)
            {
                OrderedSet<TNode> reverseNeighbors = nodeToReverseNeighbors[node];

                int maxHere;
                if (reverseNeighbors.Count == 0)
                    maxHere = 0;
                else
                    maxHere = reverseNeighbors.Select(neighbor => maxPathLengthEndingAtNode[neighbor]).Max() + 1;

                maxPathLengthEndingAtNode.Add(node, maxHere);

                foreach (TNode neighbor in nodeToNeighbors[node])
                {
                    if (neighbor.Equals(node))
                        continue;

                    if (maxPathLengthEndingAtNode.ContainsKey(neighbor))
                        throw new InvalidOperationException($"Edges cannot point to previous nodes (should be in topological order). {node} occurs after {neighbor} but points to it.");

                    nodeToReverseNeighbors[neighbor].Add(node);
                }
            }

            int maxPathLength = maxPathLengthEndingAtNode.Count == 0
                                ? 0
                                : maxPathLengthEndingAtNode.Values.Max();

            return maxPathLength;
        }
    }
}
