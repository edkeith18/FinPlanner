window.finPlanner = window.finPlanner || {};
// Two frames give the browser a paint opportunity before Blazor resumes
// synchronous work on the WebAssembly main thread.
window.finPlanner.afterPaint = () => new Promise(resolve => {
    requestAnimationFrame(() => requestAnimationFrame(() => setTimeout(resolve, 0)));
});
