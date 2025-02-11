import{d as L,x as b,c as _,N as l,M as y,q as C,v,cq as h,b as p,o as x,e as P,w as a,k as s,p as m,t as M,n as T,_ as j}from"./index-B7jvAjdE.js";import{b as u,c as d,M as r,a as o}from"./metaListUtils-B-xQ0HnD.js";import{g as O}from"./scanJobs-A4fyB_UL.js";import{M as J}from"./MetaGeneratedContent-DU2kF4fJ.js";const B=L({__name:"PastScanJobsCard",setup(w,{expose:n}){n();const{data:S}=b(O()),e=_(()=>[u.asDynamicFilterSet(S.value?.jobHistory,"title",t=>t.jobTitle),new u("phase",[new d("Stopped",t=>t.phase==="Stopped"),new d("Cancelled",t=>t.phase==="Cancelled"),new d("Other",t=>t.phase!=="Stopped"&&t.phase!=="Cancelled")]),new u("running-time",[new d("Fast",t=>l.fromISO(t.endTime).diff(l.fromISO(t.startTime)).hours<=1),new d("Slow",t=>l.fromISO(t.endTime).diff(l.fromISO(t.startTime)).hours>1)])]),f=[new r("Start time","startTime",o.Ascending),new r("Start time","startTime",o.Descending),new r("End time","endTime",o.Ascending),new r("End time","endTime",o.Descending),new r("Title","jobTitle",o.Ascending),new r("Title","jobTitle",o.Descending)];function g(t){return t.id}const c={databaseScanJobsData:S,filterSets:e,sortOptions:f,getItemKey:g,get DateTime(){return l},computed:_,get MetaListFilterOption(){return d},get MetaListFilterSet(){return u},get MetaListSortDirection(){return o},get MetaListSortOption(){return r},get MBadge(){return y},get MCollapse(){return C},get MListItem(){return v},get useSubscription(){return b},get notificationPhaseDisplayString(){return h},get getAllScanJobsSubscriptionOptions(){return O},MetaGeneratedContent:J};return Object.defineProperty(c,"__isScriptSetup",{enumerable:!1,value:!0}),c}});function F(w,n,S,e,f,g){const c=p("meta-time"),t=p("meta-duration"),D=p("meta-abbreviate-number"),I=p("meta-list-card");return x(),P(I,{title:"Past Scan Jobs",icon:"clipboard-list",itemList:e.databaseScanJobsData.jobHistory,getItemKey:e.getItemKey,filterSets:e.filterSets,sortOptions:e.sortOptions,defaultSortOption:1,emptyMessage:"No scan jobs yet.","data-testid":"past-scan-jobs-card"},{"item-card":a(i=>[s(e.MCollapse,{extraMListItemMargin:"","data-testid":"scan-jobs-entry"},{header:a(()=>[s(e.MListItem,{noLeftPadding:""},{"top-right":a(()=>[s(e.MBadge,null,{default:a(()=>[m(M(e.notificationPhaseDisplayString(i.item.phase)),1)],void 0,!0),_:2},1024)]),"bottom-left":a(()=>[T("span",null,[n[0]||(n[0]=m("Started ")),s(c,{date:i.item.startTime},null,8,["date"]),n[1]||(n[1]=m(" and lasted for ")),s(t,{duration:e.DateTime.fromISO(i.item.endTime).diff(e.DateTime.fromISO(i.item.startTime))},null,8,["duration"]),n[2]||(n[2]=m("."))])]),"bottom-right":a(()=>[T("span",null,[n[3]||(n[3]=m("Items scanned: ")),s(D,{value:i.item.scanStatistics.numItemsScanned},null,8,["value"])])]),default:a(()=>[m(M(i.item.jobTitle),1)],void 0,!0),_:2},1024)]),default:a(()=>[s(e.MetaGeneratedContent,{class:"tw-text-xs",value:i.item.scanStatistics},null,8,["value"])],void 0,!0),_:2},1024)]),_:1},8,["itemList","filterSets"])}const V=j(B,[["render",F],["__file","PastScanJobsCard.vue"]]);export{V as default};
