import{d as E,v as b,a8 as h,o as C,e as x,w as a,n as p,p as u,t as m,k as s,_ as A,x as w,c as v,b as S}from"./index-B7jvAjdE.js";import{M as O,a as N}from"./MViewContainer-BTeE2xDT.js";import{M as V}from"./MTwoColumnLayout-C4tHp69a.js";import{M as l,a as c,b as M}from"./metaListUtils-B-xQ0HnD.js";import{g as L}from"./analyticsEvents-CRLHbRhO.js";const D=E({__name:"AnalyticsTypeListGroupItem",props:{event:{type:Object,required:!0}},setup(y,{expose:n}){n();const t={get MListItem(){return b},get MTextButton(){return h}};return Object.defineProperty(t,"__isScriptSetup",{enumerable:!1,value:!0}),t}}),F={class:"tw-text-neutral-500"},I={class:"tw-font-mono tw-text-xs"};function k(y,n,t,e,_,f){return C(),x(e.MListItem,null,{"top-right":a(()=>[p("span",F,[n[0]||(n[0]=u("Code: ")),p("span",I,m(t.event.typeCode),1)])]),"bottom-left":a(()=>[u(m(t.event.docString||"No description provided."),1)]),"bottom-right":a(()=>[s(e.MTextButton,{to:`/analyticsEvents/${t.event.typeCode}`,"data-testid":"analytics-details-link"},{default:a(()=>n[1]||(n[1]=[u("View details")]),void 0,!0),_:1},8,["to"])]),default:a(()=>[u(m(t.event.categoryName)+" "+m(t.event.displayName),1)],void 0,!0),_:1})}const P=A(D,[["render",k],["__file","AnalyticsTypeListGroupItem.vue"]]),B=E({__name:"AnalyticsEventListView",setup(y,{expose:n}){n();const{data:t,error:e}=w(L()),_=[new l("Category","categoryName",c.Ascending),new l("Category","categoryName",c.Descending),new l("Name","displayName",c.Ascending),new l("Name","displayName",c.Descending)],f=["categoryName","displayName","docString","typeCode","eventType"],d=v(()=>[M.asDynamicFilterSet(t.value,"category",i=>i.categoryName)]);function r(i){return i.type.startsWith("Metaplay.")}const o=v(()=>Object.values(t.value??{}).filter(i=>r(i))),T=v(()=>Object.values(t.value??{}).filter(i=>!r(i))),g={allAnalyticsEventsData:t,allAnalyticsEventsError:e,sortOptions:_,searchFields:f,filterSets:d,isCoreEvent:r,coreEvents:o,customEvents:T,computed:v,get MetaListFilterSet(){return M},get MetaListSortDirection(){return c},get MetaListSortOption(){return l},get MPageOverviewCard(){return O},get MViewContainer(){return N},get MTwoColumnLayout(){return V},get useSubscription(){return w},AnalyticsTypeListGroupItem:P,get getAllAnalyticsEventsSubscriptionOptions(){return L}};return Object.defineProperty(g,"__isScriptSetup",{enumerable:!1,value:!0}),g}});function j(y,n,t,e,_,f){const d=S("meta-list-card"),r=S("meta-raw-data");return C(),x(e.MViewContainer,{permission:"api.analytics_events.view","is-loading":!e.allAnalyticsEventsData,error:e.allAnalyticsEventsError},{overview:a(()=>[s(e.MPageOverviewCard,{title:"View Analytics Event Types","data-testid":"analytics-event-list-overview-card"},{default:a(()=>n[0]||(n[0]=[p("p",null,"These are all the currently implemented server-side analytics events.",-1),p("div",{class:"tw-text-xs+ tw-text-neutral-500"},"You can use this page to explore and inspect the individual analytics events. This might be especially useful for data analysts who need to know the exact contents and formatting of events.",-1)]),void 0,!0),_:1})]),default:a(()=>[s(e.MTwoColumnLayout,null,{default:a(()=>[s(d,{title:"Core Event Types",icon:"list",itemList:e.coreEvents,searchFields:e.searchFields,filterSets:e.filterSets,sortOptions:e.sortOptions,pageSize:20,"data-testid":"analytics-event-list-core-events-card"},{"item-card":a(o=>[s(e.AnalyticsTypeListGroupItem,{event:o.item},null,8,["event"])]),_:1},8,["itemList","filterSets"]),s(d,{title:"Custom Event Types",icon:"list",itemList:e.customEvents,searchFields:e.searchFields,filterSets:e.filterSets,sortOptions:e.sortOptions,emptyMessage:"No custom analytics events have been registered in the game.",pageSize:20,"data-testid":"analytics-event-list-custom-events-card"},{"item-card":a(o=>[s(e.AnalyticsTypeListGroupItem,{event:o.item},null,8,["event"])]),_:1},8,["itemList","filterSets"])],void 0,!0),_:1}),s(r,{kvPair:e.allAnalyticsEventsData,name:"analyticsEvents"},null,8,["kvPair"])],void 0,!0),_:1},8,["is-loading","error"])}const H=A(B,[["render",j],["__file","AnalyticsEventListView.vue"]]);export{H as default};
