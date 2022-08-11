# Simple_XY_FWHM_UDOC
User-defined operand written in C# to calculate the FWHM from a Detector Rectangle in answer to the Zemax Community question: https://community.zemax.com/got-a-question-7/how-to-know-spot-size-for-as-each-tilt-decenter-at-detector-in-nsc-mode-3161?postid=9817#post9817

This operand has a single input parameter (Hx): the Detector Number (note that I have only tested with Detector Rectangle so far).

This operand returns up to 6 parameters:

1. X-FWHM [lens unit]
2. Y-FWHM [lens unit]
3. X-sigma [lens unit]
4. Y-sigma [lens unit]
5. X centroid location [lens unit]
6. y centroid location [lens unit]

More details can be found in the community thread.
