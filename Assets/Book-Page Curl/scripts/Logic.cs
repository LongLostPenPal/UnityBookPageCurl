using System;
using System.Collections.Generic;
using UnityEngine;

public class Logic : MonoBehaviour
{
    Book book;
    Dictionary<int , GameObject> items = new Dictionary<int , GameObject>();
    string[] prefabName = new string[]
    {
        "PageItem1",
        "PageItem2",
        "PageItem3",
        "PageItem4",
    };
    // Start is called before the first frame update
    void Start()
    {
        book = GetComponentInChildren<Book>();
        //book.Init(4 , book.GetScaleFactor() , getPageItemByIndex , b , c);
        book.Init(4 , 2.275f , getPageItemByIndex , b , c);
    }

    private void c(string obj)
    {
    }

    private void b(string obj)
    {
    }

    private GameObject getPageItemByIndex(int index)
    {
        if(!items.ContainsKey(index))
        {
           var item =  book.GetPageItemPrefab(prefabName[index],true);
            //TODO init Item
            items.Add(index , item);
        }
        return items[index];
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
