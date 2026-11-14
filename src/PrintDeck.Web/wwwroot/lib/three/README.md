Vendored three.js r170 (MIT, see LICENSE) for the 3D G-code viewer, served locally so the viewer
works on printers without internet access. Files are unmodified copies from the npm package:

- `three.module.min.js` — `three@0.170.0/build/three.module.min.js`
- `addons/controls/OrbitControls.js` — `three@0.170.0/examples/jsm/controls/OrbitControls.js`

The bare imports `three` and `three/addons/…` resolve through the import map in
`Components/App.razor`. Keep the version in step with `@types/three` in `package.json`.
