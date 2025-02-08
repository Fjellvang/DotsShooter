# Grid

This is a simple grid based spatial partitioning system. It divides the space into a grid of cells and assigns objects to these cells. This allows for fast querying of objects in a given area.
the approach is quite naive, we could consider using sparse grid to reduce memory usage.

a quadtree was considered, but to reduce random memory access, we decided to go with a grid.


## Update

We're currently unsure whether this is the best approach. We should consider comparing with a quadtree implementation.