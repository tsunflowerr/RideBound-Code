// Where the fast simulator of the thesis (Chapter 5) finds its data. The thesis-time scripts named fixed folders of the
// machine that ran them; in this copy those folder names are the only change, and they all come from here.
// RB_DATA: the data root (layout in src/fastsim/README.md: wf5/, wf6/, wf7/, wf14/, wf15/, wf17/, wf20/, wf21/).
'use strict';
const path = require('path');
if (!process.env.RB_DATA) throw new Error('RB_DATA is not set: the data root of the fast simulator (see src/fastsim/README.md)');
module.exports = { DATA: path.resolve(process.env.RB_DATA).replace(/\\/g, '/') };
