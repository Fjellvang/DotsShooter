import{d as f,x as p,v as k,a8 as h,b,o as y,e as L,w as n,k as u,p as o,t as r,n as m,_ as O}from"./index-B7jvAjdE.js";import{M as e,a}from"./metaListUtils-B-xQ0HnD.js";import{g as _}from"./matchmaking-3j4jzqL_.js";const S=f({__name:"AsyncMatchmakerListCard",setup(g,{expose:i}){i();const{data:d}=p(_()),t=["id","data.name","data.description"],l=[new e("Name","data.name",a.Descending),new e("Name","data.name",a.Ascending),new e("Participants","data.playersInBuckets",a.Ascending),new e("Participants","data.playersInBuckets",a.Descending),new e("Fill rate","data.bucketsOverallFillPercentage",a.Ascending),new e("Fill rate","data.bucketsOverallFillPercentage",a.Descending)],c={allMatchmakersData:d,searchFields:t,sortOptions:l,get MetaListSortDirection(){return a},get MetaListSortOption(){return e},get MListItem(){return k},get MTextButton(){return h},get useSubscription(){return p},get getAllMatchmakersSubscriptionOptions(){return _}};return Object.defineProperty(c,"__isScriptSetup",{enumerable:!1,value:!0}),c}});function v(g,i,d,t,l,c){const M=b("meta-list-card");return y(),L(M,{itemList:t.allMatchmakersData,searchFields:t.searchFields,sortOptions:t.sortOptions,title:"Async Matchmakers",emptyMessage:"No async matchmakers to list!","data-testid":"async-matchmakers-list-card"},{"item-card":n(({item:s})=>[u(t.MListItem,null,{"top-right":n(()=>[o(r(s.id),1)]),"bottom-left":n(()=>[o(r(s.data.description),1)]),"bottom-right":n(()=>[m("div",null,r(s.data.playersInBuckets)+" participants",1),m("div",null,r(Math.round(s.data.bucketsOverallFillPercentage*1e4)/100)+"% full",1),u(t.MTextButton,{to:`/matchmakers/${s.id}`,"data-testid":"view-matchmaker"},{default:n(()=>i[0]||(i[0]=[o("View matchmaker")]),void 0,!0),_:2},1032,["to"])]),default:n(()=>[o(r(s.data.name),1)],void 0,!0),_:2},1024)]),_:1},8,["itemList"])}const B=O(S,[["render",v],["__file","AsyncMatchmakerListCard.vue"]]);export{B as default};
