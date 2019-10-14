﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using com.rtm;

public class Main : MonoBehaviour {

    public interface ITestCase {

        void StartTest(byte[] fileBytes);
        void StopTest();
    }

    private ITestCase _testCase;

    void Start() {
        RTMRegistration.Register();
        byte[] fileBytes = null;
        // fileBytes = LoadFile(Application.dataPath + "/StreamingAssets/key/test-secp256k1-public.der");
        //SingleClientSend
        // this._testCase = new SingleClientSend();
        //SingleClientConcurrency
        this._testCase = new SingleClientConcurrency();
        //SingleClientPush
        // this._testCase = new SingleClientPush();
        //TestCase
        // this._testCase = new TestCase(777779, "5F2C13B910CDB4B12C96A12FF0978C76");
        this._testCase.StartTest(fileBytes);
    }

    byte[] LoadFile(string filePath) {
        FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        byte[] bytes = new byte[fs.Length];
        fs.Read(bytes, 0, bytes.Length);
        fs.Close();
        return bytes;
    }

    void Update() {}

    void OnApplicationQuit() {
        if (this._testCase != null) {
            this._testCase.StopTest();
        }
    }

    void OnApplicationPause() {
        if (this._testCase != null) {
            this._testCase.StopTest();
        }
    }
}
