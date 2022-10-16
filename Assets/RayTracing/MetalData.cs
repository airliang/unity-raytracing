using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MetalData
{
    public enum MetalType
    {
        custom,
        aC,
        Ag,
        Al,
        AlAs,
        AlSb,
        Au,
        Be,
        Cr,
        CsI,
        Cu,
        Cu2O,
        CuO,
        dC,
        Hg,
        HgTe,
        Ir,
        K,
        Li,
        MgO,
        Mo,
        Na,
        Nb,
        Ni,
        Rh,
        See,
        Se,
        SiC,
        SnTe,
        Ta,
        Tee,
        Te,
        ThF4,
        TiC,
        TiN,
        TiO2e,
        TiO2,
        VC,
        VN,
        V,
        W,
    }

    public struct MetalIOR
    {
        public MetalType metalType;
        public Vector3 eta;
        public Vector3 k;
    }

    public static Dictionary<string, MetalIOR> metalIORs = new Dictionary<string, MetalIOR>();
    public static Dictionary<MetalType, string> metalTypes = new Dictionary<MetalType, string>();

    public static void InitializeData()
    {
        metalTypes.Add(MetalType.custom, "Custom");
        metalIORs.Add("a-C", new MetalIOR()
        {
            metalType = MetalType.aC,
            eta = new Vector3(2.9440999183f, 2.2271502925f, 1.9681668794f),
            k = new Vector3(0.8874329109f, 0.7993216383f,  0.8152862927f)
        });
        metalTypes.Add(MetalType.aC, "a-C");
        metalIORs.Add("Ag", new MetalIOR()
        {
            metalType = MetalType.Ag,
            eta = new Vector3(2.9440999183f, 2.2271502925f, 1.9681668794f),
            k = new Vector3(0.8874329109f, 0.7993216383f, 0.8152862927f)
        });
        metalTypes.Add(MetalType.Ag, "Ag");
        metalIORs.Add("Al", new MetalIOR()
        {
            metalType = MetalType.Al,
            eta = new Vector3(1.6574599595f, 0.8803689579f, 0.5212287346f),
            k = new Vector3(9.2238691996f, 6.2695232477f, 4.8370012281f)
        });
        metalTypes.Add(MetalType.Al, "Al");
        metalIORs.Add("AlAs", new MetalIOR()
        {
            metalType = MetalType.AlAs,
            eta = new Vector3(3.6051023902f, 3.2329365777f, 2.2175611545f),
            k = new Vector3(0.0006670247f, -0.0004999400f, 0.0074261204f)
        });
        metalTypes.Add(MetalType.AlAs, "AlAs");
        metalIORs.Add("AlSb", new MetalIOR()
        {
            metalType = MetalType.AlSb,
            eta = new Vector3(-0.0485225705f, 4.1427547893f, 4.6697691348f),
            k = new Vector3(-0.0363741915f, 0.0937665154f, 1.3007390124f)
        });
        metalTypes.Add(MetalType.AlSb, "AlSb");
        metalIORs.Add("Au", new MetalIOR()
        {
            metalType = MetalType.Au,
            eta = new Vector3(0.1431189557f, 0.3749570432f, 1.4424785571f),
            k = new Vector3(3.9831604247f, 2.3857207478f, 1.6032152899f)
        });
        metalTypes.Add(MetalType.Au, "Au");
        metalIORs.Add("Be", new MetalIOR()
        {
            metalType = MetalType.Be,
            eta = new Vector3(0.1431189557f, 0.3749570432f, 1.4424785571f),
            k = new Vector3(3.8354398268f, 3.0101260162f, 2.8690088743f)
        });
        metalTypes.Add(MetalType.Be, "Be");
        metalIORs.Add("Cr", new MetalIOR()
        {
            metalType = MetalType.Cr,
            eta = new Vector3(4.3696828663f, 2.9167024892f, 1.6547005413f),
            k = new Vector3(5.2064337956f, 4.2313645277f, 3.7549467933f)
        });
        metalTypes.Add(MetalType.Cr, "Cr");
        metalIORs.Add("CsI", new MetalIOR()
        {
            metalType = MetalType.CsI,
            eta = new Vector3(2.1449030413f, 1.7023164587f, 1.6624194173f),
            k = Vector3.zero
        });
        metalTypes.Add(MetalType.CsI, "CsI");
        metalIORs.Add("Cu", new MetalIOR()
        {
            metalType = MetalType.Cu,
            eta = new Vector3(0.2004376970f, 0.9240334304f, 1.1022119527f),
            k = new Vector3(3.9129485033f, 2.4528477015f, 2.1421879552f)
        });
        metalTypes.Add(MetalType.Cu, "Cu");
        metalIORs.Add("Cu2O", new MetalIOR()
        {
            metalType = MetalType.Cu,
            eta = new Vector3(3.5492833755f, 2.9520622449f, 2.7369202137f),
            k = new Vector3(0.1132179294f, 0.1946659670f, 0.6001681264f)
        });
        metalTypes.Add(MetalType.Cu2O, "Cu2O");
        metalIORs.Add("CuO", new MetalIOR()
        {
            metalType = MetalType.CuO,
            eta = new Vector3(3.2453822204f, 2.4496293965f, 2.1974114493f),
            k = new Vector3(0.5202739621f, 0.5707372756f, 0.7172250613f)
        });
        metalTypes.Add(MetalType.CuO, "CuO");
        metalIORs.Add("d-C", new MetalIOR()
        {
            metalType = MetalType.dC,
            eta = new Vector3(2.7112524747f, 2.3185812849f, 2.2288565009f),
            k = Vector3.zero
        });
        metalTypes.Add(MetalType.dC, "d-C");
        metalIORs.Add("Hg", new MetalIOR()
        {
            metalType = MetalType.Hg,
            eta = new Vector3(2.3989314904f, 1.4400254917f, 0.9095512090f),
            k = new Vector3(6.3276269444f, 4.3719414152f, 3.4217899270f)
        });
        metalTypes.Add(MetalType.Hg, "Hg");
        metalIORs.Add("HgTe", new MetalIOR()
        {
            metalType = MetalType.HgTe,
            eta = new Vector3(4.7795267752f, 3.2309984581f, 2.6600252401f),
            k = new Vector3(1.6319827058f, 1.5808189339f, 1.7295753852f)
        });
        metalTypes.Add(MetalType.HgTe, "HgTe");
        metalIORs.Add("Ir", new MetalIOR()
        {
            metalType = MetalType.Ir,
            eta = new Vector3(3.0864098394f, 2.0821938440f, 1.6178866805f),
            k = new Vector3(5.5921510077f, 4.0671757150f, 3.2672611269f)
        });
        metalTypes.Add(MetalType.Ir, "Ir");
        metalIORs.Add("K", new MetalIOR()
        {
            metalType = MetalType.K,
            eta = new Vector3(0.0640493070f, 0.0464100621f, 0.0381842017f),
            k = new Vector3(2.1042155920f, 1.3489364357f, 0.9132113889f)
        });
        metalTypes.Add(MetalType.K, "K");
        metalIORs.Add("Li", new MetalIOR()
        {
            metalType = MetalType.Li,
            eta = new Vector3(0.2657871942f, 0.1956102432f, 0.2209198538f),
            k = new Vector3(3.5401743407f, 2.3111306542f, 1.6685930000f)
        });
        metalTypes.Add(MetalType.Li, "Li");
        metalIORs.Add("MgO", new MetalIOR()
        {
            metalType = MetalType.MgO,
            eta = new Vector3(2.0895885542f, 1.6507224525f, 1.5948759692f),
            k = Vector3.zero
        });
        metalTypes.Add(MetalType.MgO, "MgO");
        metalIORs.Add("Mo", new MetalIOR()
        {
            metalType = MetalType.Mo,
            eta = new Vector3(4.4837010280f, 3.5254578255f, 2.7760769438f),
            k = new Vector3(4.1111307988f, 3.4208716252f, 3.1506031404f)
        });
        metalTypes.Add(MetalType.Mo, "Mo");
        metalIORs.Add("Na", new MetalIOR()
        {
            metalType = MetalType.Na,
            eta = new Vector3(0.0602665320f, 0.0561412435f, 0.0619909494f),
            k = new Vector3(3.1792906496f, 2.1124800781f, 1.5790940266f)
        });
        metalTypes.Add(MetalType.Na, "Na");
        metalIORs.Add("Nb", new MetalIOR()
        {
            metalType = MetalType.Nb,
            eta = new Vector3(3.4201353595f, 2.7901921379f, 2.3955856658f),
            k = new Vector3(3.4413817900f, 2.7376437930f, 2.5799132708f)
        });
        metalTypes.Add(MetalType.Nb, "Nb");
        metalIORs.Add("Ni", new MetalIOR()
        {
            metalType = MetalType.Ni,
            eta = new Vector3(2.3672753521f, 1.6633583302f, 1.4670554172f),
            k = new Vector3(4.4988329911f, 3.0501643957f, 2.3454274399f)
        });
        metalTypes.Add(MetalType.Ni, "Ni");
        metalIORs.Add("Rh", new MetalIOR()
        {
            metalType = MetalType.Rh,
            eta = new Vector3(2.5857954933f, 1.8601866068f, 1.5544279524f),
            k = new Vector3(6.7822927110f, 4.7029501026f, 3.9760892461f)
        });
        metalTypes.Add(MetalType.Rh, "Rh");
        metalIORs.Add("Se-e", new MetalIOR()
        {
            metalType = MetalType.See,
            eta = new Vector3(5.7242724833f, 4.1653992967f, 4.0816099264f),
            k = new Vector3(0.8713747439f, 1.1052845009f, 1.5647788766f)
        });
        metalTypes.Add(MetalType.See, "Se-e");
        metalIORs.Add("Se", new MetalIOR()
        {
            metalType = MetalType.Se,
            eta = new Vector3(4.0592611085f, 2.8426947380f, 2.8207582835f),
            k = new Vector3(0.7543791750f, 0.6385150558f, 0.5215872029f)
        });
        metalTypes.Add(MetalType.Se, "Se");
        metalIORs.Add("SiC", new MetalIOR()
        {
            metalType = MetalType.SiC,
            eta = new Vector3(3.1723450205f, 2.5259677964f, 2.4793623897f),
            k = new Vector3(0.0000007284f, -0.0000006859f, 0.0000100150f)
        });
        metalTypes.Add(MetalType.SiC, "SiC");
        metalIORs.Add("SnTe", new MetalIOR()
        {
            metalType = MetalType.SnTe,
            eta = new Vector3(4.5251865890f, 1.9811525984f, 1.2816819226f),
            k = Vector3.zero
        });
        metalTypes.Add(MetalType.SnTe, "SnTe");
        metalIORs.Add("Ta", new MetalIOR()
        {
            metalType = MetalType.Ta,
            eta = new Vector3(2.0625846607f, 2.3930915569f, 2.6280684948f),
            k = new Vector3(2.4080467973f, 1.7413705864f, 1.9470377016f)
        });
        metalTypes.Add(MetalType.Ta, "Ta");
        metalIORs.Add("Te-e", new MetalIOR()
        {
            metalType = MetalType.Tee,
            eta = new Vector3(7.5090397678f, 4.2964603080f, 2.3698732430f),
            k = new Vector3(5.5842076830f, 4.9476231084f, 3.9975145063f)
        });
        metalTypes.Add(MetalType.Tee, "Te-e");
        metalIORs.Add("Te", new MetalIOR()
        {
            metalType = MetalType.Te,
            eta = new Vector3(7.3908396088f, 4.4821028985f, 2.6370708478f),
            k = new Vector3(3.2561412892f, 3.5273908133f, 3.2921683116f)
        });
        metalTypes.Add(MetalType.Te, "Te");
        metalIORs.Add("ThF4", new MetalIOR()
        {
            metalType = MetalType.ThF4,
            eta = new Vector3(1.8307187117f, 1.4422274283f, 1.3876488528f),
            k = Vector3.zero
        });
        metalTypes.Add(MetalType.ThF4, "ThF4");
        metalIORs.Add("TiC", new MetalIOR()
        {
            metalType = MetalType.TiC,
            eta = new Vector3(3.7004673762f, 2.8374356509f, 2.5823030278f),
            k = new Vector3(3.2656905818f, 2.3515586388f, 2.1727857800f)
        });
        metalTypes.Add(MetalType.TiC, "TiC");
        metalIORs.Add("TiN", new MetalIOR()
        {
            metalType = MetalType.TiN,
            eta = new Vector3(1.6484691607f, 1.1504482522f, 1.3797795097f),
            k = new Vector3(3.3684596226f, 1.9434888540f, 1.1020123347f)
        });
        metalTypes.Add(MetalType.TiN, "TiN");
        metalIORs.Add("TiO2-e", new MetalIOR()
        {
            metalType = MetalType.TiO2e,
            eta = new Vector3(3.1065574823f, 2.5131551146f, 2.5823844157f),
            k = new Vector3(0.0000289537f, -0.0000251484f, 0.0001775555f)
        });
        metalTypes.Add(MetalType.TiO2e, "TiO2-e");
        metalIORs.Add("TiO2", new MetalIOR()
        {
            metalType = MetalType.TiO2,
            eta = new Vector3(3.4566203131f, 2.8017076558f, 2.9051485020f),
            k = new Vector3(0.0001026662f, -0.0000897534f, 0.0006356902f)
        });
        metalTypes.Add(MetalType.TiO2, "TiO2");
        metalIORs.Add("VC", new MetalIOR()
        {
            metalType = MetalType.VC,
            eta = new Vector3(3.6575665991f, 2.7527298065f, 2.5326814570f),
            k = new Vector3(3.0683516659f, 2.1986687713f, 1.9631816252f)
        });
        metalTypes.Add(MetalType.VC, "VC");
        metalIORs.Add("VN", new MetalIOR()
        {
            metalType = MetalType.VN,
            eta = new Vector3(2.8656011588f, 2.1191817791f, 1.9400767149f),
            k = new Vector3(3.0323264950f, 2.0561075580f, 1.6162930914f)
        });
        metalTypes.Add(MetalType.VN, "VN");
        metalIORs.Add("V", new MetalIOR()
        {
            metalType = MetalType.V,
            eta = new Vector3(4.2775126218f, 3.5131538236f, 2.7611257461f),
            k = new Vector3(3.4911844504f, 2.8893580874f, 3.1116965117f)
        });
        metalTypes.Add(MetalType.V, "V");
        metalIORs.Add("W", new MetalIOR()
        {
            metalType = MetalType.W,
            eta = new Vector3(4.3707029924f, 3.3002972445f, 2.9982666528f),
            k = new Vector3(3.5006778591f, 2.6048652781f, 2.2731930614f)
        });
        metalTypes.Add(MetalType.W, "W");
    }

    public static MetalIOR GetMetalIOR(string metal)
    {
        if (metalIORs.Count == 0)
            InitializeData();

        return metalIORs[metal];
    }

    public static string GetMetalName(MetalType metalType)
    {
        if (metalIORs.Count == 0)
            InitializeData();
        string name = "Custom";
        if (metalTypes.TryGetValue(metalType, out name))
        {
            return name;
        }
        return name;
    }

    public static string[] GetMetalNames()
    {
        if (metalIORs.Count == 0)
            InitializeData();
        
        var enums = Enum.GetValues(typeof(MetalData.MetalType));
        string[] names = new string[enums.Length];
        int index = 0;
        foreach (var enumValue in enums)
        {
            names[index++] = GetMetalName((MetalType)enumValue);
        }
        return names;
    }
}
