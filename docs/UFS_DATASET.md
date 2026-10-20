# Authentic SASL research source

The optional place-name baseline uses the University of the Free State dataset:

- Loth, Chrismi-Rinda; Phalime, Lethabo; Sibanda, Patrick; Siyavoshi, Sara; Lombaard, Susanna; Matabane, Emily; et al. (2025), *SASL Place-Name Signs – Research Data Collection*, University of the Free State, https://doi.org/10.38140/ufs.29376494.v2
- License: CC BY-NC-SA 4.0.
- Dataset status: authentic SASL research data re-recorded by Deaf signers and subjected to internal accuracy checks, but explicitly not an official terminology list.

`ml/train_ufs_place_names.py` retrieves selected videos directly from the publisher's Figshare API and records the source file names and MD5 values in the derived model. Raw videos are ignored by Git. Any distributed derivative must retain attribution, remain non-commercial, and use the same license.

The baseline is deliberately described as a research demonstration. It is not sufficient for deployment: signer-held-out evaluation, two-hand/body/face features, more repetitions per variant, and review with a qualified SASL/Deaf-community partner are still required.
